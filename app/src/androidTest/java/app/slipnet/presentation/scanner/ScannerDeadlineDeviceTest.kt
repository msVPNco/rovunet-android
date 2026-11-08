package app.slipnet.presentation.scanner

import android.os.SystemClock
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.repository.ResolverScannerRepositoryImpl
import app.slipnet.domain.model.DnsTransport
import app.slipnet.domain.model.ResolverStatus
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.util.concurrent.atomic.AtomicInteger
import kotlin.concurrent.thread
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test

class ScannerDeadlineDeviceTest {
    @Test fun advancedUsesOneDeadlineAndCancellationClosesAWaitingQuery() = runBlocking {
        val repository = ResolverScannerRepositoryImpl(InstrumentationRegistry.getInstrumentation().targetContext)
        val seen = AtomicInteger()
        val peer = DatagramSocket(0, InetAddress.getByName("127.0.0.1"))
        val responder = thread(name = "scanner-deadline-fixture") {
            try {
                while (!peer.isClosed) {
                    val packet = DatagramPacket(ByteArray(4096), 4096)
                    peer.receive(packet)
                    if (seen.incrementAndGet() == 1) {
                        // Only the first reachability query receives a valid NXDOMAIN.
                        // Six capability checks then encounter a silent resolver.
                        val reply = packet.data.copyOf(packet.length)
                        reply[2] = 0x81.toByte(); reply[3] = 0x83.toByte()
                        peer.send(DatagramPacket(reply, reply.size, packet.socketAddress))
                    }
                }
            } catch (_: java.net.SocketException) { }
        }
        try {
            val started = SystemClock.elapsedRealtime()
            val result = repository.scanResolver("127.0.0.1", peer.localPort, "fixture.invalid", 1500, 0, DnsTransport.UDP)
            assertEquals(ResolverStatus.WORKING, result.status)
            assertFalse(requireNotNull(result.tunnelTestResult).diagnosticsComplete)
            assertTrue("Sequential capability tests must share the resolver deadline", SystemClock.elapsedRealtime() - started < 3500)
            assertTrue(seen.get() > 1)
            val waiting = launch { repository.scanResolver("127.0.0.1", peer.localPort, "fixture.invalid", 15000, 0, DnsTransport.UDP) }
            delay(150)
            val stopped = SystemClock.elapsedRealtime()
            waiting.cancelAndJoin()
            val cleanupMs = SystemClock.elapsedRealtime() - stopped
            assertTrue("Closing a scan took $cleanupMs ms; it cannot wait for a 15-second socket timeout", cleanupMs < 1500)
        } finally { peer.close(); responder.join(2000) }
        assertFalse(responder.isAlive)
    }
}
