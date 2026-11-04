package app.slipnet.core

import app.slipnet.util.ConnectionDiagnostics
import org.json.JSONObject
import java.io.File
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket

/** One owned child. Config stays on stdin; readiness requires a real local SOCKS response. */
class CoreProcess {
    @Volatile private var child: Process? = null
    private var outputThread: Thread? = null

    private fun alive(process: Process): Boolean = try {
        process.exitValue()
        false
    } catch (_: IllegalThreadStateException) { true }

    private fun awaitExit(process: Process, timeoutMs: Long): Boolean {
        val deadline = System.nanoTime() + timeoutMs * 1_000_000
        while (alive(process)) {
            val remaining = deadline - System.nanoTime()
            if (remaining <= 0) return false
            Thread.sleep(minOf(25L, (remaining / 1_000_000).coerceAtLeast(1)))
        }
        return true
    }

    @Synchronized
    fun start(binary: File, config: String, launcher: File? = null, startupTimeoutMs: Long = 10_000): Result<Unit> = runCatching {
        stop()
        ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_START)
        require(binary.isFile && binary.canExecute()) { "Xray binary missing for this ABI" }
        require(startupTimeoutMs in 100..60_000)
        val port = try {
            val inbound = JSONObject(config).getJSONArray("inbounds").getJSONObject(0)
            require(inbound.getString("listen") == "127.0.0.1" && inbound.getString("protocol") == "socks")
            inbound.getInt("port").also { require(it in 1..65535) }
        } catch (_: Exception) { error("Invalid local core configuration") }
        // Do not attach to an unrelated listener while the child is still starting.
        ServerSocket().use { socket ->
            socket.reuseAddress = true
            socket.bind(InetSocketAddress("127.0.0.1", port))
        }
        val command = mutableListOf<String>()
        launcher?.let {
            require(it.isFile && it.canExecute()) { "Core lifecycle helper missing" }
            command.add(it.absolutePath)
        }
        command.addAll(listOf(binary.absolutePath, "run", "-c", "stdin:"))
        val process = ProcessBuilder(command).redirectErrorStream(true).start()
        child = process
        outputThread = Thread({
            runCatching {
                process.inputStream.use { input ->
                    val bytes = ByteArray(4096)
                    while (input.read(bytes) >= 0) { /* discard untrusted raw diagnostics */ }
                }
            }
        }, "veyra-core-output").apply { isDaemon = true; start() }
        try {
            process.outputStream.bufferedWriter().use { it.write(config) }
            val deadline = System.nanoTime() + startupTimeoutMs * 1_000_000
            while (true) {
                check(alive(process)) { "Core rejected configuration or could not bind its port" }
                val ready = runCatching {
                    Socket().use { socket ->
                        socket.connect(InetSocketAddress("127.0.0.1", port), 100)
                        socket.soTimeout = 200
                        // Offer both supported methods; a password challenge is readiness,
                        // not successful authentication or verified Internet connectivity.
                        socket.getOutputStream().apply { write(byteArrayOf(5, 2, 0, 2)); flush() }
                        val input = socket.getInputStream()
                        input.read() == 5 && input.read() in setOf(0, 2)
                    }
                }.getOrDefault(false)
                if (ready && alive(process)) break
                check(System.nanoTime() < deadline) { "Core proxy startup timed out" }
                check(!awaitExit(process, 25)) { "Core stopped during startup" }
            }
            ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_READY)
        } catch (e: Exception) { stop(); throw e }
    }

    fun isRunning(): Boolean = child?.let(::alive) == true

    @Synchronized
    fun stop() {
        val process = child ?: return
        process.destroy()
        if (!awaitExit(process, 500)) {
            // Reflection keeps the core usable on Android versions predating
            // Process.destroyForcibly(). The native supervisor forwards termination.
            runCatching { Process::class.java.getMethod("destroyForcibly").invoke(process) }
            check(awaitExit(process, 1500)) { "Previous core did not stop" }
        }
        runCatching { process.inputStream.close() }
        outputThread?.join(1500)
        check(outputThread?.isAlive != true) { "Previous core output reader did not stop" }
        outputThread = null
        child = null
        ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_STOP)
    }
}
