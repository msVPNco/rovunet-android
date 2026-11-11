package app.slipnet.domain.model

/** An explicit path choice must never silently become an automatic collection scan. */
object ConnectionChoice {
    enum class CardTap { SELECT, CONNECT, IGNORE }

    fun cardTap(selected: Boolean, busy: Boolean, state: ConnectionState, targetIsConnected: Boolean): CardTap = when {
        busy || state.isConnecting || state is ConnectionState.Disconnecting -> CardTap.IGNORE
        !selected -> CardTap.SELECT
        targetIsConnected -> CardTap.IGNORE
        else -> CardTap.CONNECT
    }

    sealed interface Target {
        data object Automatic : Target
        data object Missing : Target
        data class Direct(val profile: ServerProfile) : Target
    }

    fun resolve(manualId: Long?, profiles: List<ServerProfile>): Target =
        if (manualId == null) Target.Automatic
        else profiles.firstOrNull { it.id == manualId }?.let { Target.Direct(it) } ?: Target.Missing

    /** Choosing a collection is not a request to start a ping/HTTPS scan. */
    fun initialPath(profiles: List<ServerProfile>, results: Map<Long, PingResult>, now: Long = System.currentTimeMillis()): ServerProfile? =
        ProfileCollections.ordered(profiles.filter { p ->
            !p.isExpired && !p.needsCredentials && !ProfileCollections.isDns(p) &&
                (results[p.id] as? PingResult.Error)?.let { !it.currentNetwork || now - it.measuredAt !in 0..300_000 } != false
        }, results).firstOrNull()

    /** A collection remembers its last explicit/connected path even after another collection is selected. */
    fun rememberedPath(collection: String?, choices: Map<String, Long>, profiles: List<ServerProfile>): Long? =
        choices[collection]?.takeIf { id -> profiles.any { it.id == id && ProfileCollections.key(it) == collection } }

    /** While connected, never substitute another path's historical measurement. */
    fun summaryProfileId(profiles: List<ServerProfile>, results: Map<Long, PingResult>,
                         connectedId: Long?, manualId: Long?): Long? {
        if (connectedId != null) return connectedId.takeIf { id -> profiles.any { it.id == id } }
        if (manualId != null) return manualId.takeIf { id -> profiles.any { it.id == id } }
        return profiles.filter { (results[it.id] as? PingResult.Success)?.verified == true }
            .minByOrNull { (results[it.id] as PingResult.Success).latencyMs }?.id
    }
}
