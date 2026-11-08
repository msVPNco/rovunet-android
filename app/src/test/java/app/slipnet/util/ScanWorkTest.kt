package app.slipnet.util

import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test

class ScanWorkTest {
    @Test fun aProvenPathPublishesBeforeTheOtherTransportFinishes() = runBlocking {
        val slow = CompletableDeferred<Unit>()
        val first = CompletableDeferred<Map<String, Boolean>>()
        val result = async {
            scanTransports(listOf("udp", "tcp"), { if (!first.isCompleted) first.complete(it) }) {
                if (it == "tcp") slow.await()
                true
            }
        }
        assertEquals(mapOf("udp" to true), withTimeout(1000) { first.await() })
        assertFalse(result.isCompleted)
        slow.complete(Unit)
        assertEquals(setOf("udp", "tcp"), result.await().keys)
    }
    @Test fun largeListsStayBoundedAndProcessEachItemOnce() = runBlocking {
        val active = AtomicInteger()
        val peak = AtomicInteger()
        val seen = java.util.concurrent.ConcurrentHashMap.newKeySet<Int>()
        scanWorkers((0 until 10_000).toList(), 3) { item ->
            val count = active.incrementAndGet()
            peak.updateAndGet { maxOf(it, count) }
            yield()
            assertTrue(seen.add(item))
            active.decrementAndGet()
        }
        assertEquals(10_000, seen.size)
        assertTrue(peak.get() <= 3)
        assertEquals(0, active.get())
    }
    @Test fun stoppingWaitsForBothTransportFinalizers() = runBlocking {
        val active = AtomicInteger()
        val ready = CompletableDeferred<Unit>()
        val job = launch {
            scanTransports(listOf(1, 2)) {
                active.incrementAndGet()
                if (active.get() == 2) ready.complete(Unit)
                try { awaitCancellation() } finally { active.decrementAndGet() }
            }
        }
        withTimeout(1000) { ready.await() }
        withTimeout(1000) { job.cancelAndJoin() }
        assertEquals(0, active.get())
    }
}
