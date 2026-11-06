package app.slipnet.tunnel

import kotlinx.coroutines.launch
import com.jcraft.jsch.JSch
import com.jcraft.jsch.ProxySOCKS5
import java.io.DataInputStream
import java.io.IOException
import java.net.InetAddress
import java.net.ServerSocket
import java.net.SocketTimeoutException
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import org.junit.Assert.*
import org.junit.Test

class SshStartupTest {
    @Test fun cancellingTheShippedSocksHandshakeClosesItsSocketWithoutWaitingForThirtySeconds() {
        ServerSocket(0).use { server ->
            val pool = Executors.newFixedThreadPool(2)
            val greeted = CountDownLatch(1)
            val owner = SshStartup()
            try {
                val peer = pool.submit<Boolean> {
                    server.accept().use { socket ->
                        socket.soTimeout = 3000
                        val input = DataInputStream(socket.getInputStream())
                        check(input.readUnsignedByte() == 5)
                        input.readFully(ByteArray(input.readUnsignedByte()))
                        greeted.countDown()
                        input.read() == -1
                    }
                }
                val session = JSch().getSession("synthetic-user", "127.0.0.1", 22)
                owner.ownSession(session)
                session.setSocketFactory(owner.socketFactory(30_000))
                session.setProxy(owner.ownProxy(ProxySOCKS5("127.0.0.1", server.localPort)))
                val connecting = pool.submit<Boolean> { runCatching { session.connect(30_000) }.isFailure }
                assertTrue(greeted.await(2, TimeUnit.SECONDS))
                owner.cancel()
                assertTrue(connecting.get(2, TimeUnit.SECONDS))
                assertTrue(peer.get(2, TimeUnit.SECONDS))
                assertEquals(0, owner.socketCount)
                owner.cancel()
            } finally { owner.cancel(); pool.shutdownNow() }
        }
    }

    @Test fun aCancelledOwnerCannotRegisterOrPublishLateResources() {
        val old = SshStartup()
        old.cancel()
        assertThrows(IOException::class.java) { old.socketFactory(100).createSocket("127.0.0.1", 9) }
        assertThrows(IOException::class.java) { old.transfer() }
        assertEquals(0, old.socketCount)
        ServerSocket(0).use { server ->
            val current = SshStartup()
            current.socketFactory(1000).createSocket("127.0.0.1", server.localPort).use { client ->
                server.accept().use { peer ->
                    old.cancel()
                    client.getOutputStream().write(42)
                    assertEquals(42, peer.getInputStream().read())
                    assertFalse(client.isClosed)
                }
            }
            current.cancel()
        }
    }

    @Test fun cancellationReleasesTheCallerEvenWhenTheOsResolverIgnoresInterruption() {
        val owner = SshStartup()
        val pool = Executors.newSingleThreadExecutor()
        val started = CountDownLatch(1)
        val release = CountDownLatch(1)
        try {
            val connecting = pool.submit<Boolean> {
                runCatching {
                    owner.socketFactory(30_000) {
                        started.countDown()
                        var waiting = true
                        while (waiting) {
                            try { release.await(); waiting = false } catch (_: InterruptedException) { }
                        }
                        InetAddress.getByAddress(byteArrayOf(127, 0, 0, 1))
                    }.createSocket("synthetic.example.invalid", 9)
                }.isFailure
            }
            assertTrue(started.await(2, TimeUnit.SECONDS))
            owner.cancel()
            assertTrue(connecting.get(2, TimeUnit.SECONDS))
            assertEquals(0, owner.socketCount)
        } finally { release.countDown(); owner.cancel(); pool.shutdownNow() }
    }

    @Test fun nameResolutionSharesTheTcpStartupDeadline() {
        val owner = SshStartup()
        val release = CountDownLatch(1)
        val start = System.nanoTime()
        try {
            assertThrows(SocketTimeoutException::class.java) {
                owner.socketFactory(150) {
                    release.await()
                    InetAddress.getByAddress(byteArrayOf(127, 0, 0, 1))
                }.createSocket("synthetic.example.invalid", 9)
            }
            assertTrue(TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - start) < 2000)
            assertEquals(0, owner.socketCount)
        } finally { release.countDown(); owner.cancel() }
    }

    @Test fun transferringAnAuthenticatedTransportPreventsAnOldCleanupFromClosingIt() {
        ServerSocket(0).use { server ->
            val owner = SshStartup()
            owner.socketFactory(1000).createSocket("127.0.0.1", server.localPort).use { client ->
                server.accept().use { peer ->
                    owner.transfer()
                    owner.cancel()
                    assertEquals(0, owner.socketCount)
                    client.getOutputStream().write(43)
                    assertEquals(43, peer.getInputStream().read())
                    assertThrows(IOException::class.java) { owner.socketFactory(100).createSocket("127.0.0.1", 9) }
                }
            }
        }
    }
    @Test fun serviceTeardownAbortsBlockingIoBeforeJoiningTheCancelledJob() = kotlinx.coroutines.runBlocking {
        ServerSocket(0).use { server ->
            val token = StartupCancellation()
            val owner = SshStartup(token)
            val pool = Executors.newSingleThreadExecutor()
            val greeted = CountDownLatch(1)
            try {
                val peer = pool.submit<Boolean> {
                    server.accept().use { socket ->
                        socket.soTimeout = 3000
                        val input = DataInputStream(socket.getInputStream())
                        check(input.readUnsignedByte() == 5)
                        input.readFully(ByteArray(input.readUnsignedByte()))
                        greeted.countDown()
                        input.read() == -1
                    }
                }
                val connecting = launch(kotlinx.coroutines.Dispatchers.IO) {
                    val session = JSch().getSession("synthetic-user", "127.0.0.1", 22)
                    owner.ownSession(session)
                    session.setSocketFactory(owner.socketFactory(30_000))
                    session.setProxy(owner.ownProxy(ProxySOCKS5("127.0.0.1", server.localPort)))
                    runCatching { session.connect(30_000) }
                }
                assertTrue(kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) { greeted.await(2, TimeUnit.SECONDS) })
                val abort = token.requestCancel()
                connecting.cancel()
                kotlinx.coroutines.withTimeout(2000) {
                    app.slipnet.service.StartupTeardown.await(abort, connecting)
                }
                assertTrue(peer.get(2, TimeUnit.SECONDS))
                assertEquals(0, owner.socketCount)
            } finally { token.requestCancel()(); pool.shutdownNow() }
        }
    }

    @Test fun aRepeatedStopStillClosesIoWhenTheFirstCloseActionWasNeverRun() {
        ServerSocket(0).use { server ->
            val token = StartupCancellation()
            val owner = SshStartup(token)
            owner.socketFactory(1000).createSocket("127.0.0.1", server.localPort).use { socket ->
                server.accept().use { peer ->
                    peer.soTimeout = 1000
                    token.requestCancel() // Simulate a cancelled teardown coroutine that never ran its action.
                    token.requestCancel()()
                    assertTrue(socket.isClosed)
                    assertEquals(-1, peer.getInputStream().read())
                    assertEquals(0, owner.socketCount)
                }
            }
        }
    }

    @Test fun cancellingBeforeRegistrationRejectsLateStartsAndDoesNotTouchTheNextToken() {
        val retired = StartupCancellation()
        retired.requestCancel()()
        val late = SshStartup(retired)
        assertThrows(IOException::class.java) { late.socketFactory(100).createSocket("127.0.0.1", 9) }
        ServerSocket(0).use { server ->
            val current = SshStartup(StartupCancellation())
            current.socketFactory(1000).createSocket("127.0.0.1", server.localPort).use { socket ->
                server.accept().use { peer ->
                    retired.requestCancel()()
                    socket.getOutputStream().write(44)
                    assertEquals(44, peer.getInputStream().read())
                }
            }
            current.cancel()
        }
    }

}
