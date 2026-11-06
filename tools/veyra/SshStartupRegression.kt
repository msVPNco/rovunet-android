import app.slipnet.tunnel.SshTunnelInstance
import app.slipnet.util.AppLog
import java.io.DataInputStream
import java.io.File
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference
import kotlin.concurrent.thread

private class SilentTransport(val mode: String) : AutoCloseable {
    val server = ServerSocket(0)
    val arrived = CountDownLatch(1)
    val closed = CountDownLatch(1)
    private val stopping = AtomicBoolean(false)
    private val socket = AtomicReference<Socket?>()
    private val failure = AtomicReference<Throwable?>()
    private val worker = thread(isDaemon = true, name = "synthetic-silent-transport") {
        try { server.accept().use { peer ->
            socket.set(peer)
            peer.soTimeout = 5000
            val input = DataInputStream(peer.getInputStream())
            when (mode) {
                "socks", "naive" -> {
                    check(input.readUnsignedByte() == 5)
                    input.readFully(ByteArray(input.readUnsignedByte()))
                }
                "http", "http-tls", "ws" -> {
                    val header = StringBuilder()
                    while (!header.endsWith("\r\n\r\n")) {
                        check(header.length < 16384)
                        val byte = input.read(); check(byte != -1); header.append(byte.toChar())
                    }
                    check(header.startsWith(if (mode == "ws") "GET " else "CONNECT "))
                    if (mode == "http-tls") {
                        peer.getOutputStream().write("HTTP/1.1 200 OK\r\n\r\n".toByteArray())
                        peer.getOutputStream().flush()
                        check(input.readUnsignedByte() == 0x16)
                    }
                }
                "tls", "payload-tls" -> {
                    if (mode == "payload-tls") {
                        val prefix = StringBuilder()
                        while (!prefix.endsWith("\r\n")) {
                            val byte = input.read(); check(byte != -1); prefix.append(byte.toChar())
                        }
                        check(prefix.startsWith("SYNTHETIC "))
                    }
                    check(input.readUnsignedByte() == 0x16)
                }
                else -> {
                    check(input.readUnsignedByte() == 'S'.code) // SSH client banner.
                }
            }
            arrived.countDown()
            while (input.read() != -1) { }
            closed.countDown()
        } } catch (error: Throwable) {
            if (stopping.get() || (arrived.count == 0L && error is java.net.SocketException)) {
                closed.countDown()
            } else failure.set(error)
        }
    }
    fun expectClosed() {
        check(closed.await(2, TimeUnit.SECONDS)) { "Peer socket remained open: $mode; ${failure.get()}" }
        check(failure.get() == null) { "Fixture failed: ${failure.get()}" }
    }
    override fun close() {
        stopping.set(true)
        runCatching { socket.get()?.close() }; server.close(); worker.join(2000)
    }
}

private fun start(instance: SshTunnelInstance, fixture: SilentTransport, listen: Int): Result<Unit> {
    val host = "127.0.0.1"
    val port = fixture.server.localPort
    return when (fixture.mode) {
        "socks", "naive" -> instance.startOverSocks5Proxy(host, 22, "synthetic-user", "synthetic-only",
            host, port, null, null, listen, naiveMode = fixture.mode == "naive")
        "dnstt" -> instance.startOverProxy(host, 22, "synthetic-user", "synthetic-only", host, port, listen)
        "http", "http-tls" -> instance.startOverHttpProxy(host, 22, "synthetic-user", "synthetic-only",
            host, port, listenPort = listen, tlsEnabled = fixture.mode == "http-tls", tlsSni = "localhost")
        "ws" -> instance.startOverWebSocket(host, port, "synthetic-user", "synthetic-only", listenPort = listen,
            wsUseTls = false)
        else -> instance.startDirect(host, port, "synthetic-user", "synthetic-only", listen,
            tlsEnabled = fixture.mode in setOf("tls", "payload-tls"), tlsSni = "localhost",
            sshPayload = if (fixture.mode == "payload-tls") "SYNTHETIC [host]:[port][crlf]" else "")
    }
}

private fun unusedPort(): Int = ServerSocket(0).use { it.localPort }

private fun stopDuring(mode: String): Long = SilentTransport(mode).use { fixture ->
    val instance = SshTunnelInstance("synthetic-$mode")
    val pool = Executors.newSingleThreadExecutor()
    val listen = unusedPort()
    try {
        val connecting = pool.submit<Result<Unit>> { start(instance, fixture, listen) }
        check(fixture.arrived.await(5, TimeUnit.SECONDS)) { "Startup did not reach $mode" }
        val at = System.nanoTime()
        instance.stop()
        check(connecting.get(2, TimeUnit.SECONDS).isFailure) { "Stopped startup reported success: $mode" }
        fixture.expectClosed()
        check(!instance.isRunning())
        check(runCatching { Socket("127.0.0.1", listen).use { } }.isFailure) { "Late listener: $mode" }
        instance.stop()
        TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - at).also { check(it < 2000) { "$mode stop: $it ms" } }
    } finally { instance.stop(); pool.shutdownNow() }
}

private fun oldFailureCannotStopNewStartup() {
    val instance = SshTunnelInstance("synthetic-replacement")
    val reachedError = CountDownLatch(1)
    val releaseError = CountDownLatch(1)
    val firstError = AtomicBoolean(true)
    val pool = Executors.newFixedThreadPool(2)
    SilentTransport("socks").use { old -> SilentTransport("socks").use { current ->
        AppLog.errorHook = { tag ->
            if (tag == "SshTunnel[synthetic-replacement]" && firstError.compareAndSet(true, false)) {
                reachedError.countDown()
                check(releaseError.await(5, TimeUnit.SECONDS))
            }
        }
        try {
            val previous = pool.submit<Result<Unit>> { start(instance, old, unusedPort()) }
            check(old.arrived.await(5, TimeUnit.SECONDS))
            instance.stop()
            check(reachedError.await(2, TimeUnit.SECONDS))
            val next = pool.submit<Result<Unit>> { start(instance, current, unusedPort()) }
            check(current.arrived.await(5, TimeUnit.SECONDS))
            releaseError.countDown()
            check(previous.get(2, TimeUnit.SECONDS).isFailure)
            check(!current.closed.await(300, TimeUnit.MILLISECONDS)) { "Old cleanup closed a newer socket" }
            check(!next.isDone)
            AppLog.errorHook = null
            instance.stop()
            check(next.get(2, TimeUnit.SECONDS).isFailure)
            current.expectClosed()
        } finally { releaseError.countDown(); AppLog.errorHook = null; instance.stop(); pool.shutdownNow() }
    } }
}

fun main() {
    val modes = listOf("direct", "dnstt", "socks", "naive", "http", "ws", "tls", "payload-tls", "http-tls")
    // Warm the actual JSch/TLS providers before measuring all process descriptors.
    modes.forEach { stopDuring(it) }
    oldFailureCannotStopNewStartup()
    val fd = File("/proc/self/fd")
    check(fd.isDirectory) { "This Linux contract lane requires /proc/self/fd" }
    val before = requireNotNull(fd.list()).size
    val durations = modes.associateWith { stopDuring(it) }
    val after = requireNotNull(fd.list()).size
    check(after == before) { "FD delta ${after - before}: before=$before after=$after" }
    println("PASS actual SSH startup cancellation: $durations ms; no late listeners; idempotent Stop; FD delta=0")
    println("PASS stale SSH startup failure cannot stop its replacement; cancellation reaches TCP/TLS/HTTP/WS/payload phases")
}
