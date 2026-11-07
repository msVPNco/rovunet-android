package app.slipnet.tunnel

import app.slipnet.core.LocalSocksEndpoint
import app.slipnet.core.ProxySocketFactory
import java.io.DataInputStream
import java.net.Authenticator
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import org.junit.Assert.*
import org.junit.Test

class ProxySocketFactoryTest {
    @Test fun authenticatesPerSocketAndSendsTheOriginalHostnameWithoutLocalDns() {
        val original = Authenticator.getDefault()
        val executor = Executors.newSingleThreadExecutor()
        try { ServerSocket(0).use { server ->
            val peer = executor.submit<String> {
                server.accept().use { socket ->
                    socket.soTimeout = 2000
                    val input = DataInputStream(socket.getInputStream()); val output = socket.getOutputStream()
                    assertEquals(listOf(5, 1, 2), List(3) { input.readUnsignedByte() })
                    output.write(5); output.flush(); output.write(2); output.flush()
                    assertEquals(1, input.readUnsignedByte())
                    val user = ByteArray(input.readUnsignedByte()).also(input::readFully)
                    val password = ByteArray(input.readUnsignedByte()).also(input::readFully)
                    assertEquals("fixture", String(user)); assertEquals("secret", String(password))
                    output.write(byteArrayOf(1, 0)); output.flush()
                    assertEquals(listOf(5, 1, 0, 3), List(4) { input.readUnsignedByte() })
                    val host = ByteArray(input.readUnsignedByte()).also(input::readFully)
                    assertEquals(443, input.readUnsignedShort())
                    output.write(byteArrayOf(5, 0, 0, 1, 127, 0, 0, 1, 0, 80)); output.flush()
                    assertEquals(112, input.readUnsignedByte()); output.write(107); output.flush()
                    String(host)
                }
            }
            ProxySocketFactory(LocalSocksEndpoint(server.localPort, "fixture", "secret")).createSocket().use { socket ->
                socket.soTimeout = 2000
                socket.connect(InetSocketAddress(InetAddress.getByAddress("example.invalid", byteArrayOf(0, 0, 0, 1)), 443), 2000)
                socket.getOutputStream().write(112); socket.getOutputStream().flush()
                assertEquals(107, socket.getInputStream().read())
            }
            assertEquals("example.invalid", peer.get(3, TimeUnit.SECONDS))
            assertSame(original, Authenticator.getDefault())
        } } finally { executor.shutdownNow() }
    }
    @Test fun rejectedAuthenticationClosesTheSocketWithoutConnectingDirectly() {
        val executor = Executors.newSingleThreadExecutor()
        try { ServerSocket(0).use { server ->
            val peer = executor.submit {
                server.accept().use { socket ->
                    val input = DataInputStream(socket.getInputStream()); val output = socket.getOutputStream()
                    input.readFully(ByteArray(3)); output.write(byteArrayOf(5, -1)); output.flush()
                    socket.soTimeout = 2000
                    assertEquals(-1, input.read())
                }
            }
            val socket = ProxySocketFactory(LocalSocksEndpoint(server.localPort, "fixture", "secret")).createSocket()
            assertTrue(runCatching { socket.connect(InetSocketAddress.createUnresolved("example.invalid", 443), 1000) }.isFailure)
            assertTrue(socket.isClosed)
            peer.get(3, TimeUnit.SECONDS)
        } } finally { executor.shutdownNow() }
    }
}
