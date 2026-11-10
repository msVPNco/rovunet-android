package app.slipnet.core

import app.slipnet.util.ConnectionDiagnostics.Failure

/** Categorize bounded child output; never persist the original line, address, key or credentials. */
object CoreRuntimeFailure {
    fun category(line: String): Failure {
        val text = line.lowercase()
        if (!text.contains("[warning]") && !text.contains("[error]")) return Failure.NONE
        return when {
            "certificate" in text || "tls handshake" in text -> Failure.TLS
            "authentication failed" in text || "invalid password" in text -> Failure.AUTH_REJECTED
            "no such host" in text || "failed to resolve" in text -> Failure.DNS_LOOKUP
            "timeout" in text || "deadline exceeded" in text -> Failure.TIMEOUT
            "connection refused" in text -> Failure.CONNECTION_REFUSED
            "connection reset" in text || "unexpected eof" in text -> Failure.REMOTE_CLOSED
            else -> Failure.NONE
        }
    }
}
