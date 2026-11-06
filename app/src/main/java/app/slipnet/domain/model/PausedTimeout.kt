package app.slipnet.domain.model

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.launch

/** Network budgets exclude bounded, explicit identity decisions, but external cancellation still wins. */
object PausedTimeout {
    private class BudgetExpired : CancellationException("Active network budget expired")

    suspend fun awaitExpiration(budgetMs: Long, pausedNanos: () -> Long = { 0L }) {
        require(budgetMs > 0)
        val started = System.nanoTime()
        val previouslyPaused = pausedNanos()
        while (true) {
            currentCoroutineContext().ensureActive()
            val elapsed = (System.nanoTime() - started - (pausedNanos() - previouslyPaused)).coerceAtLeast(0L)
            val remaining = budgetMs - elapsed / 1_000_000
            if (remaining <= 0) return
            delay(minOf(remaining, 50L))
        }
    }

    suspend fun <T> runOrNull(budgetMs: Long, pausedNanos: () -> Long = { 0L }, block: suspend () -> T): T? = coroutineScope {
        val task = async { block() }
        val timer = launch {
            awaitExpiration(budgetMs, pausedNanos)
            task.cancel(BudgetExpired())
        }
        try {
            task.await()
        } catch (_: BudgetExpired) {
            currentCoroutineContext().ensureActive()
            null
        } finally {
            timer.cancel()
        }
    }
}
