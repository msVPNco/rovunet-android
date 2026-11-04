package app.slipnet.core

/** Only fixed diagnostics leave the probe; arbitrary exception text may contain credentials. */
object ProbeFailure {
    /** Classify service startup errors without exposing hosts, usernames or credentials. */
    fun connection(message: String): String {
        val lower = message.lowercase()
        return when {
            "auth" in lower || "credential" in lower || "password" in lower -> "Connection authentication failed"
            "expired" in lower -> "Profile expired"
            "bound" in lower && "device" in lower -> "Profile belongs to another device"
            "timed out" in lower || "timeout" in lower -> "Connection setup timed out"
            "resolve" in lower || "unknownhost" in lower -> "Connection host lookup failed"
            "refused" in lower -> "Connection refused by endpoint"
            "address already in use" in lower || "port already in use" in lower -> "Local proxy port is busy"
            "certificate" in lower || "handshake" in lower -> "Connection handshake failed"
            else -> "Tunnel startup failed"
        }
    }
    private val safe = setOf("Invalid SOCKS response", "SOCKS authentication required", "Invalid SOCKS credentials",
        "SOCKS authentication failed", "SOCKS authentication rejected", "Tunnel CONNECT failed",
        "Invalid SOCKS address", "Empty HTTPS response", "Unexpected HTTPS response",
        "Connection failed", "Connection changed during test")
    fun describe(error: Throwable): String = when (error) {
        is javax.net.ssl.SSLHandshakeException -> "TLS handshake or certificate validation failed"
        is javax.net.ssl.SSLException -> "TLS connection failed"
        is java.net.SocketTimeoutException -> "Tunnel request timed out"
        is java.net.ConnectException -> "Local proxy connection failed"
        is java.io.EOFException -> "Tunnel closed before response"
        else -> error.message?.takeIf { it in safe || Regex("Unexpected HTTPS status: [0-9]{3}").matches(it) }
            ?: if (error is java.net.SocketException) "Tunnel socket closed or deadline exceeded" else "Tunnel test failed (${error.javaClass.simpleName.take(60)})"
    }
}
