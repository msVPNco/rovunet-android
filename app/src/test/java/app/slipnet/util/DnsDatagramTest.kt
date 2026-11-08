package app.slipnet.util

import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import kotlin.concurrent.thread
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test

class DnsDatagramTest {
    private fun socket() = DatagramSocket(0, InetAddress.getByName("127.0.0.1"))

    @Test fun connectedDatagramReturnsThePeerResponse() = runBlocking {
        val peer = socket()
        val responder = thread {
            val packet = DatagramPacket(ByteArray(128), 128)
            peer.receive(packet)
            peer.send(DatagramPacket(packet.data, packet.length, packet.socketAddress))
        }
        try {
            val payload = "synthetic-dns-query".toByteArray()
            val result = requireNotNull(DnsDatagram.exchange("127.0.0.1", peer.localPort, payload, 2000, 128))
            assertArrayEquals(payload, result.first.copyOf(result.second))
        } finally { peer.close(); responder.join(2000) }
    }
    @Test fun silentPeerRespectsOneDeadline() = runBlocking {
        socket().use { peer ->
            val started = System.nanoTime()
            assertNull(DnsDatagram.exchange("127.0.0.1", peer.localPort, byteArrayOf(1), 150, 128))
            val elapsed = (System.nanoTime() - started) / 1_000_000
            assertTrue("Deadline returned after $elapsed ms", elapsed in 100..2000)
        }
    }
    @Test fun cancellationWakesAFifteenSecondReceiveAndJoinsCleanup() = runBlocking {
        repeat(3) {
            val peer = socket()
            val received = CompletableDeferred<Unit>()
            val responder = thread { peer.receive(DatagramPacket(ByteArray(128), 128)); received.complete(Unit) }
            val pending = launch { DnsDatagram.exchange("127.0.0.1", peer.localPort, byteArrayOf(1), 15000, 128) }
            try {
                withTimeout(2000) { received.await() }
                val started = System.nanoTime()
                withTimeout(1000) { pending.cancelAndJoin() }
                assertTrue((System.nanoTime() - started) / 1_000_000 < 1000)
            } finally { pending.cancelAndJoin(); peer.close(); responder.join(2000) }
            assertFalse(responder.isAlive)
        }
    }
}
