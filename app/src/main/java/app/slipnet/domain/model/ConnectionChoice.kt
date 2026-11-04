package app.slipnet.domain.model

/** An explicit path choice must never silently become an automatic collection scan. */
object ConnectionChoice {
    sealed interface Target {
        data object Automatic : Target
        data object Missing : Target
        data class Direct(val profile: ServerProfile) : Target
    }

    fun resolve(manualId: Long?, profiles: List<ServerProfile>): Target =
        if (manualId == null) Target.Automatic
        else profiles.firstOrNull { it.id == manualId }?.let { Target.Direct(it) } ?: Target.Missing

    /** While connected, never substitute another path's historical measurement. */
    fun summaryProfileId(profiles: List<ServerProfile>, results: Map<Long, PingResult>,
                         connectedId: Long?, manualId: Long?): Long? {
        if (connectedId != null) return connectedId.takeIf { id -> profiles.any { it.id == id } }
        if (manualId != null) return manualId.takeIf { id -> profiles.any { it.id == id } }
        return profiles.filter { (results[it.id] as? PingResult.Success)?.verified == true }
            .minByOrNull { (results[it.id] as PingResult.Success).latencyMs }?.id
    }
}
