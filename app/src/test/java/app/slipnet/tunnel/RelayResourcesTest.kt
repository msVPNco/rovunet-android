package app.slipnet.tunnel

import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import org.junit.Assert.*
import org.junit.Test

class RelayResourcesTest {
    @Test fun stopClosesBlockedSocketReadsAndTheirExecutor() {
        ServerSocket(0).use { listener ->
            Socket("127.0.0.1", listener.localPort).use { peer ->
                val owner = RelayResources()
                val socket = owner.own(listener.accept())
                val executor = owner.own(Executors.newSingleThreadExecutor())
                val started = CountDownLatch(1)
                executor.submit { started.countDown(); runCatching { socket.getInputStream().read() } }
                assertTrue(started.await(1, TimeUnit.SECONDS))
                owner.close()
                assertTrue(executor.awaitTermination(1, TimeUnit.SECONDS))
                assertTrue(socket.isClosed)
                assertEquals(0, owner.remaining())
            }
        }
    }
    @Test fun lateOldWorkersCannotLeakIntoTheReplacementOwner() {
        val old = RelayResources().apply { close() }
        val next = RelayResources()
        val late = Socket()
        try { old.own(late); fail("stopped owner accepted a socket") } catch (_: java.io.IOException) { }
        assertTrue(late.isClosed)
        val current = next.own(Socket())
        old.close()
        assertFalse(current.isClosed)
        next.close()
        assertTrue(current.isClosed)
    }
}
