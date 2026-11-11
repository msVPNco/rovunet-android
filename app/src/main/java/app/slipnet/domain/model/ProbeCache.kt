package app.slipnet.domain.model

/** Keep measurements only while the effective connection settings are unchanged. */
object ProbeCache {
    fun fresh(result: PingResult, now: Long = System.currentTimeMillis()): Boolean = when (result) {
        is PingResult.Success -> result.currentNetwork && now - result.measuredAt in 0..300_000
        is PingResult.Error -> result.currentNetwork && now - result.measuredAt in 0..300_000
        else -> false
    }
    fun historical(results: Map<Long, PingResult>): Map<Long, PingResult> = results.mapValues { (_, result) ->
        when (result) {
            is PingResult.Success -> result.copy(currentNetwork = false)
            is PingResult.Error -> result.copy(currentNetwork = false)
            else -> result
        }
    }

    fun unchanged(before: List<ServerProfile>, after: List<ServerProfile>): Set<Long> {
        val previous = before.associateBy { it.id }
        return after.filter { p -> previous[p.id]?.let { ProfileDuplicates.key(it) == ProfileDuplicates.key(p) } == true }.map { it.id }.toSet()
    }
}
