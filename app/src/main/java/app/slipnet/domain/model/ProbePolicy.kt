package app.slipnet.domain.model

/** Protocol startup is distinct from HTTPS reachability. Collection phases still enforce their total budget. */
data class ProbePolicy(val setupMs: Long, val httpsMs: Int) {
    companion object {
        /**
         * Total budget for a test of a whole collection that does not connect afterwards: every
         * profile gets its own setup and HTTPS allowance, so late entries (Hysteria 2 and WireGuard
         * are ordered last) are not cut off by the time the early failures used. Capped at 15 minutes;
         * the test can be stopped at any time.
         */
        fun testAllBudgetMs(profiles: List<ServerProfile>, detailed: Boolean = false): Long =
            profiles.sumOf { forProfile(it).let { policy -> policy.setupMs + policy.httpsMs * (if (detailed) 3 else 2) + 25_000L } }.coerceIn(90_000L, 900_000L)

        fun forProfile(profile: ServerProfile): ProbePolicy = when {
            ProfileCollections.isDns(profile) || profile.tunnelType == TunnelType.SNOWFLAKE -> ProbePolicy(60_000, 20_000)
            profile.tunnelType in setOf(TunnelType.NAIVE, TunnelType.NAIVE_SSH) -> ProbePolicy(40_000, 12_000)
            else -> ProbePolicy(30_000, 10_000)
        }
    }
}
