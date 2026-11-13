package app.slipnet.core

import app.slipnet.util.ConnectionDiagnostics
import org.json.JSONObject
import java.io.File
import java.net.InetSocketAddress
import java.net.Socket

/** One owned child. Config stays on stdin; readiness requires a real local SOCKS response. */
class CoreProcess(private val diagnostics: Boolean = true) {
    private companion object {
        val STARTED = Regex("""[0-9/]+ [0-9:.]+ \[Warning] core: Xray [0-9]+(?:\.[0-9]+)+ started\r?""")
    }

    @Volatile private var child: Process? = null
    private var outputThread: Thread? = null
    private var diagnosticOwner = 0L

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
    fun start(binary: File, config: String, launcher: File? = null, startupTimeoutMs: Long = 10_000, workDirectory: File? = null): Result<Unit> = runCatching {
        stop()
        if (diagnostics) ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_START)
        require(binary.isFile && binary.canExecute()) { "Xray binary missing for this ABI" }
        require(startupTimeoutMs in 100..60_000)
        val settings = JSONObject(config)
        val inbound = settings.getJSONArray("inbounds").getJSONObject(0)
        require(inbound.getString("listen") == "127.0.0.1" && inbound.getString("protocol") == "socks")
        val diagnosticSession = ConnectionDiagnostics.currentSessionId()
        val outboundProtocol = settings.optJSONArray("outbounds")?.optJSONObject(0)?.optString("protocol")
        if (diagnostics) when (outboundProtocol) {
            "hysteria" -> ConnectionDiagnostics.record(ConnectionDiagnostics.Event.XRAY_HYSTERIA2_START, diagnosticSession)
            "wireguard" -> ConnectionDiagnostics.record(ConnectionDiagnostics.Event.XRAY_WIREGUARD_START, diagnosticSession)
            "mieru" -> ConnectionDiagnostics.record(ConnectionDiagnostics.Event.AUXILIARY_MIERU_START, diagnosticSession)
        }
        val port = inbound.getInt("port")
        require(port in 1..65535)
        val socks = inbound.optJSONObject("settings")
        val account = if (socks?.optString("auth") == "password") socks.getJSONArray("accounts").getJSONObject(0) else null
        // The pinned core emits this warning through its own pipe only after every inbound
        // successfully binds. A reply from a port contender cannot produce that event.
        settings.put("log", JSONObject().put("loglevel", "warning").put("access", "none"))
        val listening = java.util.concurrent.CountDownLatch(1)
        val command = mutableListOf<String>()
        launcher?.let {
            require(it.isFile && it.canExecute()) { "Core lifecycle helper missing" }
            command.add(it.absolutePath)
        }
        val auxiliary = outboundProtocol in setOf("amneziawg", "anytls", "tuic", "mieru")
        val executable = if (auxiliary) File(binary.parentFile, "librovunet_awg.so") else binary
        require(executable.isFile && executable.canExecute()) { "Core binary missing for this ABI" }
        command.add(executable.absolutePath)
        if (!auxiliary) command.addAll(listOf("run", "-c", "stdin:"))
        val builder = ProcessBuilder(command).redirectErrorStream(true)
        if (auxiliary && workDirectory != null) {
            require(workDirectory.isDirectory)
            builder.environment()["TMPDIR"] = workDirectory.absolutePath
        }
        val process = builder.start()
        child = process
        diagnosticOwner = diagnosticSession
        if (diagnostics) ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_PROCESS_STARTED, diagnosticSession)
        outputThread = Thread({
            runCatching {
                process.inputStream.buffered().use { input ->
                    val line = StringBuilder(160)
                    var discard = false
                    val reported = mutableSetOf<ConnectionDiagnostics.Failure>()
                    while (true) {
                        val ch = input.read()
                        if (ch == -1) break
                        if (ch == 10) {
                            if (!discard && (if (auxiliary) line.toString() == "ROVUNET_AWG_READY" else STARTED.matches(line))) listening.countDown()
                            if (diagnostics && !discard) {
                                val category = CoreRuntimeFailure.category(line.toString())
                                if (category != ConnectionDiagnostics.Failure.NONE && reported.add(category))
                                    ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_UPSTREAM_FAILED, diagnosticSession, category)
                            }
                            line.setLength(0)
                            discard = false
                        } else if (line.length < 4096) line.append(ch.toChar()) else discard = true
                    }
                }
            }
        }, "veyra-core-output").apply { isDaemon = true; start() }
        try {
            process.outputStream.bufferedWriter().use { it.write(settings.toString()) }
            if (diagnostics) ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_CONFIG_WRITTEN, diagnosticSession)
            val deadline = System.nanoTime() + startupTimeoutMs * 1_000_000
            while (true) {
                check(alive(process)) { "Core rejected configuration or could not bind its port" }
                val ready = listening.count == 0L && runCatching {
                    Socket().use { socket ->
                        socket.connect(InetSocketAddress("127.0.0.1", port), 100)
                        socket.soTimeout = 200
                        val method = if (account == null) 0 else 2
                        val output = socket.getOutputStream()
                        val input = socket.getInputStream()
                        output.write(byteArrayOf(5, 1, method.toByte()))
                        if (input.read() != 5 || input.read() != method) return@use false
                        if (account == null) return@use true
                        val user = account.getString("user").toByteArray(Charsets.UTF_8)
                        val pass = account.getString("pass").toByteArray(Charsets.UTF_8)
                        require(user.size in 1..255 && pass.size in 1..255)
                        output.write(byteArrayOf(1, user.size.toByte()) + user + byteArrayOf(pass.size.toByte()) + pass)
                        input.read() == 1 && input.read() == 0
                    }
                }.getOrDefault(false)
                if (ready && alive(process)) break
                check(System.nanoTime() < deadline) { "Core proxy startup timed out" }
                check(!awaitExit(process, 25)) { "Core stopped during startup" }
            }
            if (diagnostics) ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_READY, diagnosticSession)
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
        if (diagnostics) ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_STOP, diagnosticOwner)
    }
}
