package app.slipnet.domain.model

/** Delays are HTTPS milliseconds, never throughput. Defaults are review-build policy. */
object SmartPathPolicy {
    const val SLOW_MS = 2_000L
    const val MIN_STAY_MS = 120_000L
    const val CYCLE_MS = 30_000L
    const val MAX_CANDIDATES = 5
    enum class Mode { MANUAL, FAILOVER, SMART }
    // Concurrent WG clients can roam a shared peer to a new UDP endpoint.
    // Keep WG-to-WG recovery serialized instead of disturbing the live peer.
    fun independentCandidatesCompatible(active: ServerProfile, candidate: ServerProfile): Boolean =
        !(ProfileCollections.isWireguard(active) && ProfileCollections.isWireguard(candidate))
    fun better(current: Long, candidate: Long, elapsedSinceSwitch: Long): Boolean =
        current >= SLOW_MS && candidate > 0 && candidate <= current / 2 && elapsedSinceSwitch >= MIN_STAY_MS
    fun median(samples: List<Long>): Long? = if (samples.size < 3 || samples.any { it <= 0 }) null else samples.sorted()[samples.size / 2]
}
