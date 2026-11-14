package app.slipnet.core

import java.io.File
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import org.json.JSONObject

/** Isolated protocol probe. A listening local SOCKS port is never handshake evidence. */
object ProtocolHandshake {
    fun supports(link: String): Boolean = runCatching {
        val out = CoreLink.parse(link).outbound
        out.getString("protocol") in setOf("tuic", "wireguard", "amneziawg") ||
            out.getString("protocol") == "hysteria" && out.optJSONObject("streamSettings")?.let { stream ->
                !stream.has("finalmask") && stream.optJSONObject("tlsSettings")?.has("cipherSuites") == false
            } == true
    }.getOrDefault(false)

    suspend fun measure(link: String, binary: File, launcher: File, certificateDirectory: File): Long = withContext(Dispatchers.IO) {
        require(supports(link)) { "Protocol handshake test unavailable" }
        require(binary.isFile && launcher.isFile) { "Protocol probe runtime missing" }
        check(certificateDirectory.isDirectory || certificateDirectory.mkdirs()) { "Certificate storage unavailable" }
        val config = CoreLink.parse(link).outbound.toString()
        val process = ProcessBuilder(launcher.absolutePath, binary.absolutePath, "--probe")
            .directory(certificateDirectory.parentFile).redirectErrorStream(true).apply {
                environment()["ROVUNET_CERTIFICATE_STORE"] = certificateDirectory.absolutePath
            }.start()
        suspendCancellableCoroutine { continuation ->
            continuation.invokeOnCancellation { process.destroy() }
            try {
                process.outputStream.bufferedWriter(Charsets.UTF_8).use { it.write(config) }
                val deadline = System.nanoTime() + 12_000_000_000L
                while (runCatching { process.exitValue(); false }.getOrDefault(true)) {
                    check(continuation.isActive && System.nanoTime() < deadline) { "Protocol handshake timed out" }
                    Thread.sleep(20)
                }
                check(process.exitValue() == 0) { "Protocol handshake failed" }
                val reader = process.inputStream.reader(Charsets.UTF_8)
                val output = CharArray(4097)
                var size = 0
                while (size < output.size) {
                    val count = reader.read(output, size, output.size - size)
                    if (count < 0) break
                    size += count
                }
                check(size < output.size) { "Invalid protocol probe response" }
                val result = JSONObject(String(output, 0, size))
                require(result.getString("kind") == "protocol-handshake") { "Protocol handshake not confirmed" }
                val elapsed = result.getLong("latencyMs")
                require(elapsed in 0..12_000) { "Invalid handshake time" }
                if (continuation.isActive) continuation.resume(elapsed)
            } catch (error: Exception) {
                if (continuation.isActive) continuation.resumeWithException(error)
            } finally {
                process.destroy()
                runCatching { process.inputStream.close() }
                runCatching { process.errorStream.close() }
                runCatching { process.outputStream.close() }
                val cleanup = System.nanoTime() + 2_000_000_000L
                while (runCatching { process.exitValue(); false }.getOrDefault(true) && System.nanoTime() < cleanup) Thread.sleep(20)
                if (runCatching { process.exitValue(); false }.getOrDefault(true))
                    runCatching { Process::class.java.getMethod("destroyForcibly").invoke(process) }
            }
        }
    }
}
