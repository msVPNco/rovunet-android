package app.slipnet.domain.model

sealed class PingResult {
    data object Pending : PingResult()
    /** E2E test in progress — shows current phase (e.g. "QUIC handshake...") */
    data class Testing(val phase: String) : PingResult()
    data class Success(val latencyMs: Long, val verified: Boolean = false, val successes: Int = 1, val attempts: Int = 1, val jitterMs: Long = 0, val setupMs: Long = 0, val measuredAt: Long = System.currentTimeMillis()) : PingResult()
    data class Error(val message: String, val measuredAt: Long = System.currentTimeMillis()) : PingResult()
    data object Skipped : PingResult()
}
