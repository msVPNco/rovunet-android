package app.slipnet.domain.model

import java.security.cert.CertificateFactory
import java.security.cert.Certificate

/** This field is stored as public metadata, so reject private keys before persistence. */
object SlipstreamIdentity {
    private val pemBlock = Regex("""\A-----BEGIN CERTIFICATE-----\s+[A-Za-z0-9+/=\s]+-----END CERTIFICATE-----\z""")
    fun certificate(pem: String): Certificate? {
        if (pem.isBlank()) return null
        require(pem.length <= 32768 && pemBlock.matches(pem.trim())) { "INVALID_SLIPSTREAM_CERTIFICATE" }
        return try { CertificateFactory.getInstance("X.509").generateCertificate(pem.byteInputStream()) }
        catch (_: Exception) { throw IllegalArgumentException("INVALID_SLIPSTREAM_CERTIFICATE") }
    }
}
