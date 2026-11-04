package app.slipnet.domain.model

/** Keep measurements only while the effective connection settings are unchanged. */
object ProbeCache {
    fun unchanged(before: List<ServerProfile>, after: List<ServerProfile>): Set<Long> {
        val previous = before.associateBy { it.id }
        return after.filter { p -> previous[p.id]?.let { ProfileDuplicates.key(it) == ProfileDuplicates.key(p) } == true }.map { it.id }.toSet()
    }
}
