package app.slipnet.domain.model

enum class ConnectionIndicator { DISCONNECTED, CONNECTING, CONNECTED, DISCONNECTING, ERROR }

sealed class ConnectionState {
    data object Disconnected : ConnectionState()
    data object Connecting : ConnectionState()
    data class Connected(
        val profile: ServerProfile,
        val connectedAt: Long = System.currentTimeMillis(),
        val chainName: String? = null,
        val chainId: Long = -1
    ) : ConnectionState()
    data object Disconnecting : ConnectionState()
    data class Error(val message: String, val cause: Throwable? = null) : ConnectionState()

    val indicator: ConnectionIndicator
        get() = when (this) {
            Disconnected -> ConnectionIndicator.DISCONNECTED
            Connecting -> ConnectionIndicator.CONNECTING
            is Connected -> ConnectionIndicator.CONNECTED
            Disconnecting -> ConnectionIndicator.DISCONNECTING
            is Error -> ConnectionIndicator.ERROR
        }

    val isConnected: Boolean
        get() = this is Connected

    val isConnecting: Boolean
        get() = this is Connecting

    val isDisconnected: Boolean
        get() = this is Disconnected || this is Error

    val displayName: String
        get() = when (this) {
            is Disconnected -> "Disconnected"
            is Connecting -> "Connecting"
            is Connected -> "Connected"
            is Disconnecting -> "Disconnecting"
            is Error -> "Error"
        }
}
