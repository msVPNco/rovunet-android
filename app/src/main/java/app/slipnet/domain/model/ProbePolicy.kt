package app.slipnet.domain.model

/** Protocol startup is distinct from HTTPS reachability. Collection phases still enforce their total budget. */
data class ProbePolicy(val setupMs: Long, val httpsMs: Int) {
    companion object {
        fun forProfile(profile: ServerProfile): ProbePolicy = when {
            ProfileCollections.isDns(profile) || profile.tunnelType == TunnelType.SNOWFLAKE -> ProbePolicy(60_000, 20_000)
            profile.tunnelType in setOf(TunnelType.NAIVE, TunnelType.NAIVE_SSH) -> ProbePolicy(40_000, 12_000)
            else -> ProbePolicy(30_000, 10_000)
        }
    }
}
