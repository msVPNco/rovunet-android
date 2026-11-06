package app.slipnet.domain.model

import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive

/** One bounded phase. Cleanup remains the probe's responsibility, including cancellation. */
object CollectionPhase {
    suspend fun run(profiles: List<ServerProfile>, connectAfter: Boolean, budgetMs: Long,
                    onProgress: (Int, ServerProfile) -> Unit,
                    onResult: (ServerProfile, PingResult) -> Unit,
                    pausedNanos: () -> Long = { 0L },
                    probe: suspend (ServerProfile) -> PingResult): Boolean {
        var passed = false
        PausedTimeout.runOrNull(budgetMs, pausedNanos) {
            for ((index, profile) in profiles.withIndex()) {
                currentCoroutineContext().ensureActive()
                onProgress(index,profile)
                val result = probe(profile)
                onResult(profile,result)
                if (result is PingResult.Success && result.verified) {
                    passed = true
                    if (connectAfter) break
                }
            }
        }
        return passed
    }
}
