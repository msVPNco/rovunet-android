package app.slipnet.tunnel

import com.jcraft.jsch.Proxy
import com.jcraft.jsch.Session
import com.jcraft.jsch.SocketFactory
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.net.InetSocketAddress
import java.net.InetAddress
import java.net.Socket
import java.net.SocketTimeoutException
import java.util.concurrent.ArrayBlockingQueue
import java.util.concurrent.FutureTask
import java.util.concurrent.ThreadPoolExecutor
import java.util.concurrent.TimeUnit
import java.util.concurrent.TimeoutException

/** Resources belong to one startup attempt, never to a reusable global cancellation flag. */
internal class SshStartup(cancellation: StartupCancellation? = null) {
    private enum class State { OPEN, CANCELLED, TRANSFERRED }
    private val lock = Any()
    private var state = State.OPEN
    private val sockets = linkedSetOf<Socket>()
    private val cleanup = mutableListOf<() -> Unit>()

    private val diagnosticSessionId = cancellation?.diagnosticSessionId ?: 0
    private var cancellationRegistration: AutoCloseable? = null
    init { cancellationRegistration = cancellation?.register { cancel() } }

    val isCancelled: Boolean get() = synchronized(lock) { state == State.CANCELLED }
    val socketCount: Int get() = synchronized(lock) { sockets.size }

    fun requireActive() = synchronized(lock) {
        if (state != State.OPEN) throw IOException("SSH startup was cancelled or completed")
    }

    fun <T : Socket> own(socket: T): T {
        val accepted = synchronized(lock) {
            if (state == State.OPEN) { sockets.add(socket); true } else false
        }
        if (!accepted) {
            runCatching { socket.close() }
            throw IOException("SSH startup was cancelled or completed")
        }
        return socket
    }

    private fun register(close: () -> Unit) {
        val accepted = synchronized(lock) {
            if (state == State.OPEN) { cleanup.add(close); true } else false
        }
        if (!accepted) {
            runCatching(close)
            throw IOException("SSH startup was cancelled or completed")
        }
    }

    fun ownSession(session: Session) { register { session.disconnect() } }
    fun <T : Proxy> ownProxy(proxy: T): T = proxy.also { register { it.close() } }

    private fun resolve(host: String, timeoutMs: Int, lookup: (String) -> InetAddress): InetAddress {
        // Numeric destinations (notably local tunnel endpoints) do not need the resolver pool.
        if (host.contains(':') || host.matches(Regex("[0-9.]+"))) return InetAddress.getByName(host)
        val task = FutureTask { lookup(host) }
        register { task.cancel(true); resolverPool.remove(task) }
        try {
            resolverPool.execute(task)
            return task.get(timeoutMs.toLong(), TimeUnit.MILLISECONDS)
        } catch (error: TimeoutException) {
            throw SocketTimeoutException("SSH name resolution timed out")
        } catch (error: Exception) {
            if (error is InterruptedException) Thread.currentThread().interrupt()
            throw IOException("SSH name resolution failed or was cancelled", error)
        } finally {
            task.cancel(true)
            resolverPool.remove(task)
        }
    }

    fun socketFactory(
        timeoutMs: Int, lookup: (String) -> InetAddress = InetAddress::getByName
    ): SocketFactory = object : SocketFactory {
        override fun createSocket(host: String, port: Int): Socket {
            val socket = own(Socket()) // Register before DNS/TCP/proxy/TLS/SSH can block.
            try {
                val deadline = System.nanoTime() + TimeUnit.MILLISECONDS.toNanos(timeoutMs.toLong())
                val address = resolve(host, timeoutMs, lookup)
                requireActive()
                val remaining = TimeUnit.NANOSECONDS.toMillis(deadline - System.nanoTime())
                if (remaining <= 0) throw SocketTimeoutException("SSH TCP connection timed out")
                socket.soTimeout = timeoutMs
                socket.tcpNoDelay = true
                socket.connect(InetSocketAddress(address, port), remaining.coerceAtMost(timeoutMs.toLong()).toInt())
                requireActive()
                return socket
            } catch (error: Exception) {
                synchronized(lock) { sockets.remove(socket) }
                runCatching { socket.close() }
                throw error
            }
        }
        override fun getInputStream(socket: Socket): InputStream = socket.getInputStream()
        override fun getOutputStream(socket: Socket): OutputStream = socket.getOutputStream()
    }

    /** Keep transport decorators (TLS/payload) and own their outer socket as well as the raw one. */
    fun decorate(factory: SocketFactory): SocketFactory = object : SocketFactory {
        override fun createSocket(host: String, port: Int): Socket = own(factory.createSocket(host, port))
        override fun getInputStream(socket: Socket): InputStream = factory.getInputStream(socket)
        override fun getOutputStream(socket: Socket): OutputStream = factory.getOutputStream(socket)
    }

    /** Called only while the instance lifecycle lock publishes the authenticated session/listener. */
    fun transfer() = synchronized(lock) {
        requireActive()
        state = State.TRANSFERRED
        cancellationRegistration?.close()
        cancellationRegistration = null
        sockets.clear()
        cleanup.clear()
    }

    fun cancel() {
        val owned = synchronized(lock) {
            if (state != State.OPEN) return
            state = State.CANCELLED
            val snapshot = sockets.toList() to cleanup.toList().asReversed()
            sockets.clear()
            cleanup.clear()
            snapshot
        }
        cancellationRegistration?.close()
        cancellationRegistration = null
        // Close raw sockets first: Session.disconnect alone cannot abort a pending SOCKS handshake.
        owned.first.forEach { runCatching { it.close() } }
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STARTUP_IO_CLOSED, diagnosticSessionId)
        owned.second.forEach { runCatching(it) }
    }

    companion object {
        // The OS resolver may ignore interruption; bound its workers/queue and never wait for it on Stop.
        private val resolverPool = ThreadPoolExecutor(
            4, 4, 30, TimeUnit.SECONDS, ArrayBlockingQueue(16),
            { task -> Thread(task, "ssh-startup-resolver").apply { isDaemon = true } }
        ).apply { allowCoreThreadTimeOut(true) }
    }
}
