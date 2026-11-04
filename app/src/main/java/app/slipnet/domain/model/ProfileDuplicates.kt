package app.slipnet.domain.model

/** Conservative equality of ALL model fields except UI/session metadata.
 * Credentials stay in memory; keys must never be logged or persisted.
 */
object ProfileDuplicates {
    data class Match(val keep: ServerProfile, val remove: ServerProfile)
    fun key(p: ServerProfile): ServerProfile = p.copy(
        id=0, name="", createdAt=0, updatedAt=0, lastConnectedAt=0,
        sortOrder=0, isActive=false, isPinned=false, collectionName="", importAccountHint="",
        coreLink=if (p.coreLink.contains("://") && !p.coreLink.trimStart().startsWith("[Interface]", true)) p.coreLink.substringBefore('#') else p.coreLink
    ).let { normalized ->
        // Native VLESS exports carry legacy SSH-host/resolver placeholders.
        // VlessBridge receives neither field; compare the actual VLESS settings.
        if (normalized.tunnelType == TunnelType.VLESS) normalized.copy(sshHost="", resolvers=emptyList()) else normalized
    }
    fun plan(profiles: List<ServerProfile>, protectedIds: Set<Long> = emptySet()): List<Match> =
        profiles.filter { !it.isLocked }.groupBy(::key).values.flatMap { group ->
            val keeper = group.firstOrNull { it.id in protectedIds || it.isPinned || it.isActive } ?: group.first()
            group.filter { it.id != keeper.id && it.id !in protectedIds && !it.isPinned && !it.isActive }
                .map { Match(keeper,it) }
        }
    /** Only called within a proposed collection; metadata from its native members wins. */
    fun withinImport(profiles: List<ServerProfile>): List<ServerProfile> {
        val seen = mutableSetOf<ServerProfile>()
        return profiles.filter { p -> p.isLocked || seen.add(key(p).copy(collectionId="")) }
    }
}
