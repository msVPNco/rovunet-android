package app.slipnet.tunnel

import android.content.Context
import java.io.File
import java.security.MessageDigest
import java.security.cert.CertificateFactory

/** Public tunnel identity only. Never substitutes a Caddy certificate or disables verification. */
object SlipstreamCertificate {
    fun materialize(context: Context, pem: String, domain: String): String {
        val directory = File(context.filesDir, "slipstream-certificates").apply {
            check(isDirectory || mkdirs()) { "Cannot create Slipstream certificate store" }
        }
        val certificate = app.slipnet.domain.model.SlipstreamIdentity.certificate(pem)
        if (certificate == null) {
            require(pem.isBlank()) { "Invalid Slipstream certificate" }
            val authority = domain.trim().trimEnd('.').lowercase(java.util.Locale.ROOT)
            require(authority.isNotBlank()) { "Missing Slipstream authority" }
            val scope = MessageDigest.getInstance("SHA-256").digest(authority.toByteArray(Charsets.UTF_8))
                .joinToString("") { "%02x".format(it) }
            // Native QUIC receives the peer certificate and atomically enrolls it only after
            // verifying CertificateVerify. Existing pins are always checked and never replaced.
            return "tofu:" + File(directory, "authority-$scope.pem").absolutePath
        }
        (certificate as java.security.cert.X509Certificate).checkValidity()
        val digest = MessageDigest.getInstance("SHA-256").digest(certificate.encoded).joinToString("") { "%02x".format(it) }
        val file = File(directory, "$digest.pem")
        val temporary = File.createTempFile("certificate-", ".pem", directory)
        try {
            temporary.writeText(pem, Charsets.UTF_8)
            check(temporary.renameTo(file)) { "Cannot store Slipstream certificate" }
        } finally { temporary.delete() }
        return file.absolutePath
    }
}
