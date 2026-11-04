import app.slipnet.core.ProbeFailure
fun main() {
 check(ProbeFailure.describe(IllegalStateException("SOCKS authentication rejected")) == "SOCKS authentication rejected")
 check(ProbeFailure.describe(IllegalStateException("Unexpected HTTPS status: 403")) == "Unexpected HTTPS status: 403")
 check(ProbeFailure.describe(javax.net.ssl.SSLHandshakeException("secret.example/password")) == "TLS handshake or certificate validation failed")
 check(!ProbeFailure.describe(IllegalStateException("vless://secret@example.org")).contains("secret"))
 check(ProbeFailure.connection("Authentication failed password=private") == "Connection authentication failed")
 check(ProbeFailure.connection("Connection timed out at private.example") == "Connection setup timed out")
 check(ProbeFailure.connection("Address already in use") == "Local proxy port is busy")
 check(ProbeFailure.connection("vless://private@example.org") == "Tunnel startup failed")
 println("PASS diagnostic categories and secret redaction")
}
