package app.slipnet.domain.model

/** Conservative equality of ALL model fields except UI/session metadata.
 * Credentials stay in memory; keys must never be logged or persisted.
 */
object ProfileDuplicates {
    data class Match(val keep: ServerProfile, val remove: ServerProfile)
    fun key(p: ServerProfile): ServerProfile = p.copy(
        id=0, name="", createdAt=0, updatedAt=0, lastConnectedAt=0,
        sortOrder=0, isActive=false, isPinned=false, collectionName="", importAccountHint="", deletionReview=null,
        coreLink=if (p.coreLink.contains("://") && !p.coreLink.trimStart().startsWith("[Interface]", true)) p.coreLink.substringBefore('#') else p.coreLink
    ).let { normalized ->
        // Native VLESS exports carry legacy SSH-host/resolver placeholders.
        // VlessBridge receives neither field; compare the actual VLESS settings.
        if (normalized.tunnelType == TunnelType.VLESS) normalized.copy(sshHost="", resolvers=emptyList()) else normalized
    }
    fun plan(profiles: List<ServerProfile>, protectedIds: Set<Long> = emptySet()): List<Match> =
        profiles.filter { !it.isLocked && !it.needsCredentials }.groupBy(::key).values.flatMap { group ->
            val keeper = group.firstOrNull { it.id in protectedIds || it.isPinned || it.isActive } ?: group.first()
            group.filter { it.id != keeper.id && it.id !in protectedIds && !it.isPinned && !it.isActive }
                .map { Match(keeper,it) }
        }
    /** Only called within a proposed collection; metadata from its native members wins. */
    fun withinImport(profiles: List<ServerProfile>): List<ServerProfile> {
        val seen = mutableSetOf<ServerProfile>()
        return profiles.filter { p -> p.needsCredentials || seen.add(key(p).copy(collectionId="")) }
    }
    /**
     * Profiles of an import that are not already in the app. A profile counts as present when every
     * connection field equals an existing profile's (name, collection and timestamps do not count),
     * so importing the same list twice adds nothing. Lock and device/expiry restrictions are part
     * of the key and are never relaxed. Unreadable credentials cannot be compared safely.
     */
    fun withoutExisting(existing: List<ServerProfile>, incoming: List<ServerProfile>): List<ServerProfile> {
        val present = existing.filterNot { it.needsCredentials }.map { key(it).copy(collectionId = "") }.toHashSet()
        return incoming.filter { p -> p.needsCredentials || present.add(key(p).copy(collectionId = "")) }
    }
}
