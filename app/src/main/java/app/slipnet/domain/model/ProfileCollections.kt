package app.slipnet.domain.model

/** Identity is explicit: names and shared/CDN addresses are never account identifiers. */
object ProfileCollections {
    fun key(p: ServerProfile): String = p.collectionId.ifBlank { "profile:${p.id}" }
    fun name(p: ServerProfile): String = p.collectionName.ifBlank { p.name }
    fun members(profiles: List<ServerProfile>, selected: ServerProfile?): List<ServerProfile> =
        if (selected == null) emptyList() else profiles.filter { key(it) == key(selected) }
    fun isDns(p: ServerProfile): Boolean = p.tunnelType in setOf(
        TunnelType.DNSTT, TunnelType.DNSTT_SSH, TunnelType.NOIZDNS, TunnelType.NOIZDNS_SSH,
        TunnelType.VAYDNS, TunnelType.VAYDNS_SSH, TunnelType.SLIPSTREAM, TunnelType.SLIPSTREAM_SSH,
        TunnelType.SOAR, TunnelType.SOAR_SSH)
    fun isUdp(p: ServerProfile): Boolean = p.tunnelType == TunnelType.XRAY &&
        (p.coreLink.substringBefore("://").lowercase() in setOf("hysteria2", "hy2", "wireguard") || p.coreLink.trimStart().startsWith("[Interface]", true))
    fun ordered(profiles: List<ServerProfile>, results: Map<Long, PingResult>): List<ServerProfile> {
        // Measured paths first inside each tier; DNS always remains the explicit fallback.
        val defaults = profiles.sortedBy { when (it.tunnelType) {
            TunnelType.XRAY -> if (isUdp(it)) 3 else 0
            TunnelType.VLESS -> 1
            TunnelType.SSH, TunnelType.NAIVE, TunnelType.NAIVE_SSH, TunnelType.SOCKS5 -> 2
            TunnelType.DOH -> 3
            TunnelType.SNOWFLAKE -> 4
            else -> 5
        } }
        val fresh = results.filterValues { it is PingResult.Success && System.currentTimeMillis() - it.measuredAt in 0..300_000 }
        return ProfileRanking.sort(defaults, fresh, true).sortedBy { if (isDns(it)) 1 else 0 }
    }
    fun suggestedName(profiles: List<ServerProfile>): String {
        val users = profiles.map { it.sshUsername }.filter { it.isNotBlank() }.distinct()
        val hosts = profiles.map { it.domain }.filter { it.isNotBlank() }.distinct()
        return listOfNotNull(users.singleOrNull(), hosts.singleOrNull()).joinToString(" — ")
            .ifBlank { profiles.firstOrNull()?.name ?: "Collection" }.take(120)
    }
}
