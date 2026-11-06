package app.slipnet.domain.model

/** Keep measurements only while the effective connection settings are unchanged. */
object ProbeCache {
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
