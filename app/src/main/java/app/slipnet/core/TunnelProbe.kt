package app.slipnet.core

import java.io.DataInputStream
import java.net.InetSocketAddress
import java.net.Socket
import java.util.Timer
import java.util.TimerTask
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import javax.net.ssl.SNIHostName
import javax.net.ssl.SSLSocket
import javax.net.ssl.SSLSocketFactory

/** HTTPS through an explicit SOCKS endpoint. Never falls back to a direct connection. */
object TunnelProbe {
    const val HOST = "www.gstatic.com"
    const val PATH = "/generate_204"
    const val FALLBACK_HOST = "cp.cloudflare.com"
    data class Measurements(val summary: ProbeSummary?, val attempts: Int, val failures: String)

    /** At most three actual requests, total. Try the other target after a failure,
     * and move on once both targets fail without a successful sample. */
    suspend fun sample(onAttempt: (Int) -> Unit, successTarget: Int = 3, request: suspend (String) -> Long): Measurements {
        require(successTarget == 1 || successTarget == 3)
        var target = HOST
        val samples = mutableListOf<Long?>()
        val failures = linkedMapOf<String, String>()
        for (attempt in 1..if (successTarget == 1) 2 else 3) {
            currentCoroutineContext().ensureActive()
            onAttempt(attempt)
            try {
                samples.add(request(target))
                if (samples.count { it != null } >= successTarget) break
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                currentCoroutineContext().ensureActive()
                samples.add(null)
                failures[target] = ProbeFailure.describe(e)
                if (failures.size == 2 && samples.none { it != null }) break
                target = if (target == HOST) FALLBACK_HOST else HOST
            }
        }
        return Measurements(ProbeSummary.of(samples), samples.size,
            failures.entries.joinToString("; ") { "${it.key}: ${it.value}" })
    }

    fun measure(port: Int, username: String? = null, password: String? = null,
                host: String = HOST, remotePort: Int = 443, path: String = PATH,
                factory: SSLSocketFactory = SSLSocketFactory.getDefault() as SSLSocketFactory,
                timeoutMs: Int = 8000): Long = Socket().use {
        measureSocket(it, port, username, password, host, remotePort, path, factory, timeoutMs)
    }

    suspend fun measureCancellable(port: Int, username: String?, password: String?, host: String,
                                   timeoutMs: Int): Long = withContext(Dispatchers.IO) {
        suspendCancellableCoroutine { continuation ->
            val socket = Socket()
            continuation.invokeOnCancellation { runCatching { socket.close() } }
            socket.use {
                if (continuation.isActive) {
                    continuation.resumeWith(runCatching {
                        measureSocket(socket, port, username, password, host, 443, PATH,
                            SSLSocketFactory.getDefault() as SSLSocketFactory, timeoutMs)
                    })
                }
            }
        }
    }

    /** A scanner-supplied URL, still through an explicit SOCKS tunnel with no direct fallback. */
    suspend fun measureUrlCancellable(port: Int, url: String, timeoutMs: Int): Long = withContext(Dispatchers.IO) {
        val uri = java.net.URI(url)
        require(uri.scheme in setOf("http", "https") && !uri.host.isNullOrBlank() && uri.userInfo == null)
        val secure = uri.scheme == "https"
        val path = uri.rawPath.orEmpty().ifEmpty { "/" } + (uri.rawQuery?.let { "?$it" } ?: "")
        require(!path.contains('\r') && !path.contains('\n'))
        suspendCancellableCoroutine { continuation ->
            val socket = Socket()
            continuation.invokeOnCancellation { runCatching { socket.close() } }
            socket.use {
                if (continuation.isActive) continuation.resumeWith(runCatching {
                    measureSocket(socket, port, null, null, uri.host.removeSurrounding("[", "]"),
                        if (uri.port > 0) uri.port else if (secure) 443 else 80, path,
                        SSLSocketFactory.getDefault() as SSLSocketFactory, timeoutMs, secure)
                })
            }
        }
    }

    private fun measureSocket(socket: Socket, port: Int, username: String?, password: String?,
                              host: String, remotePort: Int, path: String, factory: SSLSocketFactory,
                              timeoutMs: Int, secure: Boolean = true): Long {
        require(timeoutMs in 100..30000)
        val start = System.nanoTime()
        val timer = Timer("veyra-probe-deadline", true)
        timer.schedule(object : TimerTask() { override fun run() { runCatching { socket.close() } } }, timeoutMs.toLong())
        try {
            socket.connect(InetSocketAddress("127.0.0.1", port), minOf(2000, timeoutMs))
            socket.soTimeout = timeoutMs
            val input = DataInputStream(socket.getInputStream())
            val out = socket.getOutputStream()
            val auth = !username.isNullOrEmpty()
            out.write(if (auth) byteArrayOf(5, 2, 0, 2) else byteArrayOf(5, 1, 0)); out.flush()
            check(input.readUnsignedByte() == 5) { "Invalid SOCKS response" }
            when (input.readUnsignedByte()) {
                0 -> Unit
                2 -> {
                    check(auth) { "SOCKS authentication required" }
                    val user = username!!.toByteArray(); val pass = password.orEmpty().toByteArray()
                    check(user.size in 1..255 && pass.size in 1..255) { "Invalid SOCKS credentials" }
                    out.write(byteArrayOf(1, user.size.toByte()) + user + byteArrayOf(pass.size.toByte()) + pass); out.flush()
                    check(input.readUnsignedByte() == 1 && input.readUnsignedByte() == 0) { "SOCKS authentication failed" }
                }
                else -> error("SOCKS authentication rejected")
            }
            val domain = host.toByteArray(Charsets.US_ASCII)
            require(domain.size in 1..255)
            out.write(byteArrayOf(5, 1, 0, 3, domain.size.toByte()) + domain + byteArrayOf((remotePort shr 8).toByte(), remotePort.toByte())); out.flush()
            check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 0) { "Tunnel CONNECT failed" }
            check(input.readUnsignedByte() == 0)
            val size = when (input.readUnsignedByte()) { 1 -> 4; 4 -> 16; 3 -> input.readUnsignedByte(); else -> error("Invalid SOCKS address") }
            input.readFully(ByteArray(size + 2))
            val remote = if (secure) factory.createSocket(socket, host, remotePort, true) else socket
            remote.use { tls ->
                if (tls is SSLSocket) {
                    tls.sslParameters = tls.sslParameters.apply {
                        if (!host.contains(':') && !host.matches(Regex("[0-9.]+"))) serverNames = listOf(SNIHostName(host))
                        endpointIdentificationAlgorithm = "HTTPS"
                    }
                    tls.soTimeout = timeoutMs
                    tls.startHandshake()
                }
                tls.outputStream.write("GET $path HTTP/1.1\r\nHost: $host\r\nConnection: close\r\nCache-Control: no-cache\r\n\r\n".toByteArray(Charsets.US_ASCII))
                tls.outputStream.flush()
                val line = StringBuilder()
                while (line.length < 1024) {
                    val b = tls.inputStream.read(); check(b >= 0) { "Empty HTTPS response" }
                    if (b == 10) break
                    line.append(b.toChar())
                }
                check(Regex("HTTP/1\\.[01] 204(?: .*)?\\r?").matches(line.toString())) { Regex("HTTP/1\\.[01] ([0-9]{3})").find(line.toString())?.groupValues?.get(1)?.let { "Unexpected HTTPS status: $it" } ?: "Unexpected HTTPS response" }
                return (System.nanoTime() - start) / 1_000_000
            }
        } finally { timer.cancel(); runCatching { socket.close() } }
    }
}

data class ProbeSummary(val medianMs: Long, val jitterMs: Long, val successes: Int, val attempts: Int) {
    companion object {
        fun of(samples: List<Long?>): ProbeSummary? {
            val good = samples.filterNotNull().sorted()
            if (good.isEmpty()) return null
            val median = if (good.size % 2 == 1) good[good.size / 2] else good[good.size / 2 - 1] + (good[good.size / 2] - good[good.size / 2 - 1]) / 2
            return ProbeSummary(median, good.last() - good.first(), good.size, samples.size)
        }
    }
}
