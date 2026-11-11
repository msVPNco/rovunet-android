package app.slipnet.util

import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/** A large resolver list creates a bounded number of workers, not one job per IP. */
internal suspend fun <T> scanWorkers(items: List<T>, concurrency: Int, work: suspend (T) -> Unit) = coroutineScope {
    val next = AtomicInteger(0)
    List(minOf(items.size, concurrency.coerceIn(1, 100))) {
        launch {
            while (isActive) {
                val index = next.getAndIncrement()
                if (index >= items.size) break
                work(items[index])
            }
        }
    }.joinAll()
}

/** Publish each transport as it finishes; final collection still waits for cleanup of both. */
internal suspend fun <K, V> scanTransports(
    transports: List<K>,
    onProgress: suspend (Map<K, V>) -> Unit = {},
    probe: suspend (K) -> V
): Map<K, V> = coroutineScope {
    val results = linkedMapOf<K, V>()
    val update = Mutex()
    transports.map { transport ->
        async {
            val result = probe(transport)
            update.withLock {
                results[transport] = result
                onProgress(results.toMap())
            }
        }
    }.awaitAll()
    results.toMap()
}
