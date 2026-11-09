package app.slipnet.service

/** A parallel process may probe only while the owning VPN explicitly excludes our UID. */
internal object ProbeBypass {
    @Volatile private var owner = 0L
    fun established(session: Long, selfExcluded: Boolean) { owner = if (selfExcluded) session else 0 }
    fun allows(session: Long): Boolean = session > 0 && owner == session
    fun clear(session: Long) { if (owner == session) owner = 0 }
}
