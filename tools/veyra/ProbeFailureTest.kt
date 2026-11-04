import app.slipnet.core.ProbeFailure
fun main() {
 check(ProbeFailure.describe(IllegalStateException("SOCKS authentication rejected")) == "SOCKS authentication rejected")
 check(ProbeFailure.describe(IllegalStateException("Unexpected HTTPS status: 403")) == "Unexpected HTTPS status: 403")
 check(ProbeFailure.describe(javax.net.ssl.SSLHandshakeException("secret.example/password")) == "TLS handshake or certificate validation failed")
 check(!ProbeFailure.describe(IllegalStateException("vless://secret@example.org")).contains("secret"))
 println("PASS diagnostic categories and secret redaction")
}
