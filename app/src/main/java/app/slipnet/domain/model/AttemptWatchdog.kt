package app.slipnet.domain.model

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

/** Timer belongs to one attempt. Completion/cancellation cannot expire a newer attempt. */
class AttemptWatchdog(private val scope: CoroutineScope) {
    private var job: Job? = null
    fun cancel() { job?.cancel(); job = null }
    fun arm(timeoutMs: Long, stillStarting: () -> Boolean, pausedNanos: () -> Long = { 0L }, expire: () -> Unit) {
        require(timeoutMs > 0)
        cancel()
        job = scope.launch {
            PausedTimeout.awaitExpiration(timeoutMs, pausedNanos)
            if (stillStarting()) expire()
        }
    }
}
