package app.slipnet.domain.model

import java.util.concurrent.atomic.AtomicLong

/** A callback is meaningful only for the attempt that created it. */
class ConnectionSessions {
    private val sequence = AtomicLong()
    val current: Long get() = sequence.get()
    fun begin(): Long = sequence.incrementAndGet()
    fun owns(id: Long): Boolean = id > 0 && id == current
}

data class SessionConnectionState(val sessionId: Long, val state: ConnectionState)
