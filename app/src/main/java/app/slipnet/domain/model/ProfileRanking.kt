package app.slipnet.domain.model

object ProfileRanking {
    fun sort(profiles: List<ServerProfile>, results: Map<Long, PingResult>, reliability: Boolean = false): List<ServerProfile> {
        fun result(p: ServerProfile) = (results[p.id] as? PingResult.Success)?.takeIf { it.verified }
        val latency = compareBy<ServerProfile> { result(it)?.latencyMs ?: Long.MAX_VALUE }
        val freshness = compareBy<ServerProfile> { p -> result(p)?.let { if (ProbeCache.fresh(it)) 0 else 1 } ?: 2 }
        val measurement = if (reliability) compareByDescending<ServerProfile> { result(it)?.let { r -> !r.quick } == true }
        .thenByDescending {
            result(it)?.let { r -> r.successes.toDouble() / r.attempts.coerceAtLeast(1) } ?: -1.0
        }.then(latency) else latency
        return profiles.sortedWith(freshness.then(measurement))
    }
}
