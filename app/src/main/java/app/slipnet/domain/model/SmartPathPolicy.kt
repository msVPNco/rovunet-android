package app.slipnet.domain.model

/** Delays are HTTPS milliseconds, never throughput. Defaults are review-build policy. */
object SmartPathPolicy {
    const val SLOW_MS = 2_000L
    const val MIN_STAY_MS = 120_000L
    const val CYCLE_MS = 180_000L
    const val MAX_CANDIDATES = 5
    enum class Mode { MANUAL, FAILOVER, SMART }
    fun better(current: Long, candidate: Long, elapsedSinceSwitch: Long): Boolean =
        current >= SLOW_MS && candidate > 0 && candidate <= current / 2 && elapsedSinceSwitch >= MIN_STAY_MS
    fun median(samples: List<Long>): Long? = if (samples.size < 3 || samples.any { it <= 0 }) null else samples.sorted()[samples.size / 2]
}
