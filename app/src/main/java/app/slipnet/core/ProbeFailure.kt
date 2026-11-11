package app.slipnet.core

/** Only fixed diagnostics leave the probe; arbitrary exception text may contain credentials. */
object ProbeFailure {
    const val APPROVAL_REQUIRED = "Server identity approval required; connect manually to review"
    fun isLocalServiceFailure(message: String): Boolean = message in setOf(
        "Previous tunnel is still stopping; connection was not started",
        "Android could not start the VPN service",
        "Android could not start the VPN foreground service",
        "Android could not stop the VPN service"
    )
    /** Classify service startup errors without exposing hosts, usernames or credentials. */
    fun connection(message: String): String {
        val lower = message.lowercase()
        return when {
            "hostkey has been changed" in lower || "identity changed" in lower -> "Server identity changed; connection refused"
            "hostkey" in lower || "identity approval required" in lower -> APPROVAL_REQUIRED
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
        is javax.net.ssl.SSLPeerUnverifiedException -> "HTTPS peer identity verification failed"
        is javax.net.ssl.SSLHandshakeException -> {
            val causes = generateSequence<Throwable>(error) { it.cause }.take(8).toList()
            when {
                causes.any { it is java.security.cert.CertificateExpiredException } -> "HTTPS certificate expired"
                causes.any { it is java.security.cert.CertificateNotYetValidException } -> "HTTPS certificate not yet valid"
                causes.any { it is java.security.cert.CertificateException || it is java.security.cert.CertPathValidatorException } ->
                    "HTTPS certificate validation failed"
                causes.any { it is java.io.EOFException || it is java.net.SocketException } -> "Peer closed during TLS handshake"
                else -> "HTTPS handshake failed"
            }
        }
        is javax.net.ssl.SSLException -> "TLS connection failed"
        is java.net.SocketTimeoutException -> "Tunnel request timed out"
        is java.net.ConnectException -> "Local proxy connection failed"
        is java.io.EOFException -> "Tunnel closed before response"
        else -> error.message?.takeIf { it in safe || Regex("Unexpected HTTPS status: [0-9]{3}").matches(it) }
            ?: if (error is java.net.SocketException) "Tunnel socket closed or deadline exceeded" else "Tunnel test failed (${error.javaClass.simpleName.take(60)})"
    }
}
