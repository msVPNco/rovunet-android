package app.slipnet.tunnel

import android.content.Context
import java.io.File
import java.security.MessageDigest
import java.security.cert.CertificateFactory

/** Public tunnel identity only. Never substitutes a Caddy certificate or disables verification. */
object SlipstreamCertificate {
    fun materialize(context: Context, pem: String): String {
        if (pem.isBlank()) return ""
        require(pem.length <= 32768 && !pem.contains("PRIVATE KEY")) { "Invalid Slipstream public certificate" }
        val certificate = CertificateFactory.getInstance("X.509").generateCertificate(pem.byteInputStream())
        val digest = MessageDigest.getInstance("SHA-256").digest(certificate.encoded).joinToString("") { "%02x".format(it) }
        val directory = File(context.cacheDir, "slipstream-certificates").apply { mkdirs() }
        val file = File(directory, "$digest.pem")
        val temporary = File.createTempFile("certificate-", ".pem", directory)
        try {
            temporary.writeText(pem)
            check(temporary.renameTo(file)) { "Cannot store Slipstream certificate" }
        } finally { temporary.delete() }
        return file.absolutePath
    }
}
