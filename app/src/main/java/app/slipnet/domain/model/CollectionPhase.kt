package app.slipnet.domain.model

import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive

/** One bounded phase. Cleanup remains the probe's responsibility, including cancellation. */
object CollectionPhase {
    enum class Completion { INCOMPLETE, COMPLETE, NEEDS_REVIEW, OFFER_DNS, NO_VERIFIED_PATH }

    /** Pick exactly one outcome; a budget expiry must not be replaced with a generic failure. */
    fun completion(passed: Boolean, untested: Boolean, needsReview: Boolean, hasDnsFallback: Boolean, includeDnsAfterSuccess: Boolean = false): Completion = when {
        untested -> Completion.INCOMPLETE
        passed && includeDnsAfterSuccess && hasDnsFallback -> Completion.OFFER_DNS
        passed -> Completion.COMPLETE
        needsReview -> Completion.NEEDS_REVIEW
        hasDnsFallback -> Completion.OFFER_DNS
        else -> Completion.NO_VERIFIED_PATH
    }

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
