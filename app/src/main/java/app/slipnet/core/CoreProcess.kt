package app.slipnet.core

import java.io.File
import app.slipnet.util.ConnectionDiagnostics
import java.util.concurrent.TimeUnit

/** Single owned child; configuration travels over stdin, never argv or logs. */
class CoreProcess {
    @Volatile private var child: Process? = null
    @Synchronized fun start(binary: File, config: String, launcher: File? = null): Result<Unit> = runCatching {
        stop()
        ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_START)
        require(binary.isFile && binary.canExecute()) { "Xray binary missing for this ABI" }
        val command = mutableListOf<String>()
        launcher?.let { require(it.isFile && it.canExecute()) { "Core lifecycle helper missing" }; command.add(it.absolutePath) }
        command.addAll(listOf(binary.absolutePath, "run", "-c", "stdin:"))
        val p = ProcessBuilder(command).redirectErrorStream(true).start()
        child = p
        Thread({ runCatching { p.inputStream.use { input -> val bytes = ByteArray(4096); while (input.read(bytes) >= 0) { /* discard untrusted raw diagnostics */ } } } }, "veyra-core-output").apply { isDaemon = true; start() }
        try {
            p.outputStream.bufferedWriter().use { it.write(config) }
            check(!p.waitFor(200, TimeUnit.MILLISECONDS)) { "Core rejected configuration or could not bind its port" }
            ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_READY)
        }
        catch (e: Exception) { stop(); throw e }
    }
    fun isRunning(): Boolean = child?.isAlive == true
    @Synchronized fun stop() {
        val p = child ?: return
        p.destroy()
        if (!p.waitFor(500, TimeUnit.MILLISECONDS)) {
            p.destroyForcibly()
            check(p.waitFor(1500, TimeUnit.MILLISECONDS)) { "Previous core did not stop" }
        }
        child = null
        ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CORE_STOP)
    }
}
