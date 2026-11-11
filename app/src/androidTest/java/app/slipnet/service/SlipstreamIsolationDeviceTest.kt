package app.slipnet.service

import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.tunnel.SlipstreamProbePool
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test

class SlipstreamIsolationDeviceTest {
    @Test fun fourWorkersHaveIndependentProcessesAndReleaseCancelledSlots() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val ready = java.util.concurrent.atomic.AtomicInteger()
        val release = CompletableDeferred<Unit>()
        val pids = java.util.concurrent.ConcurrentHashMap.newKeySet<Int>()
        val previousOwner = TunnelOperations.owner
        assertFalse(app.slipnet.tunnel.SlipstreamBridge.isClientRunning())
        app.slipnet.tunnel.SlipstreamBridge.proxyOnlyMode = true
        val profile = app.slipnet.domain.model.ServerProfile(name = "Isolated synthetic probe", domain = "example.invalid")
        val livePort = java.net.ServerSocket(0).use { it.localPort }
        app.slipnet.tunnel.SlipstreamBridge.startClient(profile.domain,
            listOf(app.slipnet.tunnel.ResolverConfig("127.0.0.1", 9, false)), tcpListenPort = livePort).getOrThrow()
        val jobs = (0..3).map { async(Dispatchers.IO) {
            SlipstreamProbePool.use(context) { client ->
                pids.add(client.processId())
                val port = java.net.ServerSocket(0).use { it.localPort }
                client.start(profile, "127.0.0.1", 9, port).getOrThrow()
                assertTrue(client.getClientPort() > 0)
                ready.incrementAndGet(); release.await()
            }
        } }
        try {
            withTimeout(20000) { while (ready.get() < 4) delay(25) }
            assertEquals(4, pids.size)
            assertFalse(pids.contains(android.os.Process.myPid()))
            assertEquals(previousOwner, TunnelOperations.owner)
            assertTrue(app.slipnet.tunnel.SlipstreamBridge.isClientRunning())
            assertEquals(livePort, app.slipnet.tunnel.SlipstreamBridge.getClientPort())
        } finally {
            jobs.forEach { it.cancel() }; release.complete(Unit); jobs.forEach { it.join() }
            assertTrue(app.slipnet.tunnel.SlipstreamBridge.isClientRunning())
            app.slipnet.tunnel.SlipstreamBridge.stopClient()
            app.slipnet.tunnel.SlipstreamBridge.proxyOnlyMode = false
        }
        withTimeout(20000) {
            (0..3).map { async(Dispatchers.IO) { SlipstreamProbePool.use(context) { it.processId() } } }.awaitAll()
        }
        assertEquals(previousOwner, TunnelOperations.owner)
    }
    @Test fun pinnedQuicCarriesRealBytesAndRejectsAnotherCertificate() = runBlocking {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val context = instrumentation.targetContext
        val encoded = requireNotNull(InstrumentationRegistry.getArguments().getString("slipstreamFixture"))
        val fixture = org.json.JSONObject(String(android.util.Base64.decode(encoded, android.util.Base64.DEFAULT), Charsets.UTF_8))
        val profile = app.slipnet.domain.model.ServerProfile(name = "Pinned synthetic tunnel", domain = "tunnel.example.invalid",
            authoritativeMode = true, slipstreamCertificate = fixture.getString("certificate"))
        val originalOwner = TunnelOperations.owner
        withTimeout(40000) {
            (0..3).map { async(Dispatchers.IO) {
                SlipstreamProbePool.use(context) { client ->
                    val port = java.net.ServerSocket(0).use { it.localPort }
                    client.start(profile, "10.0.2.2", fixture.getInt("port"), port).getOrThrow()
                    while (!client.isQuicReady()) delay(50)
                    java.net.Socket("127.0.0.1", client.getClientPort()).use { socket ->
                        socket.soTimeout = 10000
                        val sent = ByteArray(512) { (it % 97).toByte() }
                        socket.getOutputStream().write(sent)
                        val received = ByteArray(sent.size)
                        java.io.DataInputStream(socket.getInputStream()).readFully(received)
                        assertArrayEquals(sent, received)
                    }
                }
            } }.awaitAll()
        }
        SlipstreamProbePool.use(context) { client ->
            val port = java.net.ServerSocket(0).use { it.localPort }
            val start = client.start(profile.copy(slipstreamCertificate = fixture.getString("wrongCertificate")),
                "10.0.2.2", fixture.getInt("port"), port)
            if (start.isSuccess) {
                repeat(100) { assertFalse("Mismatched public pin was accepted", client.isQuicReady()); delay(50) }
            }
        }
        assertEquals(originalOwner, TunnelOperations.owner)
    }
}
