package app.slipnet.domain.model

/** Selection evidence only: a secondary radio becoming available is not a default-route change. */
object PhysicalRouteSelection {
    fun <T> choose(active: T?, vpn: Boolean, underlying: List<T>, transport: Int?,
        available: List<Pair<T, Set<Int>>>, previous: T?): T? {
        val ids = available.map { it.first }
        if (active in ids) return active
        underlying.firstOrNull { it in ids }?.let { return it }
        val matching = available.filter { transport != null && transport in it.second }.map { it.first }
        return matching.singleOrNull() ?: previous?.takeIf { it in matching } ?:
            previous?.takeIf { vpn && transport == null && it in ids } ?: ids.singleOrNull()
    }
}
