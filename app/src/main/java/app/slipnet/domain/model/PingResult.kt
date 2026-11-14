package app.slipnet.domain.model

sealed class PingResult {
    data object Pending : PingResult()
    /** E2E test in progress — shows current phase (e.g. "QUIC handshake...") */
    data class Testing(val phase: String) : PingResult()
    data class Success(val latencyMs: Long, val verified: Boolean = false, val successes: Int = 1, val attempts: Int = 1, val jitterMs: Long = 0, val setupMs: Long = 0, val measuredAt: Long = System.currentTimeMillis(), val quick: Boolean = false, val currentNetwork: Boolean = true, val handshake: Boolean = false) : PingResult()
    data class Error(val message: String, val measuredAt: Long = System.currentTimeMillis(), val currentNetwork: Boolean = true) : PingResult()
    data object Skipped : PingResult()
    data object ReviewIdentity : PingResult()
}
