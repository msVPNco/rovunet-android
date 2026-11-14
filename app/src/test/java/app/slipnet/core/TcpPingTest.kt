package app.slipnet.core

import java.net.ServerSocket
import java.net.Socket
import java.net.SocketAddress
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test

class TcpPingTest {
    @Test fun quickPingSendsNoHttpOrTlsBytes() = runBlocking {
        ServerSocket(0).use { server ->
            val peer = async(Dispatchers.IO) { server.accept().use { it.soTimeout = 3000; it.getInputStream().read() } }
            assertTrue(TcpPing.measure("127.0.0.1", server.localPort) >= 0)
            assertEquals(-1, withTimeout(5000) { peer.await() })
        }
    }

    @Test fun cancellationClosesThePendingConnect() = runBlocking {
        val entered = CountDownLatch(1)
        val closed = CountDownLatch(1)
        val socket = object : Socket() {
            override fun connect(endpoint: SocketAddress, timeout: Int) {
                entered.countDown()
                check(closed.await(5, TimeUnit.SECONDS)) { "Connect was not cancelled" }
                throw java.net.SocketException("Closed")
            }
            override fun close() { closed.countDown(); super.close() }
        }
        val task = async(Dispatchers.IO) { TcpPing.measure("127.0.0.1", 9, socketFactory = { socket }) }
        assertTrue(entered.await(3, TimeUnit.SECONDS))
        withTimeout(2000) { task.cancelAndJoin() }
        assertTrue(socket.isClosed)
    }

    @Test fun refusedConnectAlsoReleasesItsSocket() = runBlocking {
        val socket = Socket()
        val port = ServerSocket(0).use { it.localPort }
        try { TcpPing.measure("127.0.0.1", port, socketFactory = { socket }); fail("Closed port accepted") }
        catch (_: java.net.ConnectException) { assertTrue(socket.isClosed) }
    }
}
