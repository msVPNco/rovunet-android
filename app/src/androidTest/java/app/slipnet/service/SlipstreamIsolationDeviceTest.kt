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
        val jobs = (0..3).map { async(Dispatchers.IO) {
            SlipstreamProbePool.use(context) { client ->
                pids.add(client.processId()); ready.incrementAndGet(); release.await()
            }
        } }
        try {
            withTimeout(20000) { while (ready.get() < 4) delay(25) }
            assertEquals(4, pids.size)
            assertFalse(pids.contains(android.os.Process.myPid()))
            assertEquals(previousOwner, TunnelOperations.owner)
        } finally {
            jobs.forEach { it.cancel() }; release.complete(Unit); jobs.forEach { it.join() }
        }
        withTimeout(20000) {
            (0..3).map { async(Dispatchers.IO) { SlipstreamProbePool.use(context) { it.processId() } } }.awaitAll()
        }
        assertEquals(previousOwner, TunnelOperations.owner)
    }
}
