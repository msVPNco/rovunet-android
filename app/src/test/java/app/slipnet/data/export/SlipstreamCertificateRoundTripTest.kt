package app.slipnet.data.export

import app.slipnet.domain.model.*
import org.junit.Assert.*
import org.junit.Test

class SlipstreamCertificateRoundTripTest {
    @Test fun optionalPublicIdentitySurvivesBothSlipstreamVariantsWithoutBecomingSshCredentials() {
        val cert = "-----BEGIN CERTIFICATE-----\nsynthetic-public-certificate\n-----END CERTIFICATE-----"
        for (type in listOf(TunnelType.SLIPSTREAM, TunnelType.SLIPSTREAM_SSH)) {
            val profile = ServerProfile(name = "Synthetic tunnel", domain = "example.invalid", tunnelType = type,
                resolvers = listOf(DnsResolver("192.0.2.53", 53)), slipstreamCertificate = cert,
                sshUsername = "fixture", sshPassword = "synthetic-only")
            val result = ConfigImporter().parseAndImport(ConfigExporter().exportSingleProfile(profile))
            assertTrue(result is ImportResult.Success)
            val restored = (result as ImportResult.Success).profiles.single()
            assertEquals(cert, restored.slipstreamCertificate)
            assertEquals("synthetic-only", restored.sshPassword)
            assertEquals(type, restored.tunnelType)
        }
    }
}
