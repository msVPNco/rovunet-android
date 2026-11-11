package app.slipnet.core

import app.slipnet.domain.model.*
import org.junit.Assert.*
import org.junit.Test

class Alpha19MaintenanceTest {
    private val id = "00000000-0000-4000-8000-000000000001"
    @Test fun newProtocolUrisPreserveRuntimeAcrossManualEdits() {
        val links = listOf(
            "anytls://secret%2Bvalue@example.invalid:443?sni=tls.example.invalid&idle-session-timeout=30&min-idle-session=0",
            "tuic://$id:secret%3Avalue@example.invalid:8443?sni=tls.example.invalid&alpn=h3&congestion_control=bbr&udp_relay_mode=native&reduce_rtt=0",
            "mierus://user:secret%3Avalue@example.invalid?profile=fixture&port=9000&protocol=TCP&multiplexing=MULTIPLEXING_LOW"
        )
        for (link in links) {
            val first = CoreLink.parse(link)
            assertTrue(CoreLink.handles(link))
            val edited = CoreLink.parse(CoreForm.decode(link).encode("Edited"))
            assertTrue(first.outbound.similar(edited.outbound))
            assertTrue(CoreLink.runtime(link, 12345, "local", "secret").contains("\"auth\":\"password\""))
        }
    }
    @Test fun incompatibleAndUnverifiedOptionsAreRejected() {
        for (link in listOf(
            "anytls://p@example.invalid?insecure=1",
            "anytls://p@example.invalid?plugin=script",
            "anytls://p@example.invalid?min-idle-session=1000",
            "tuic://bad:p@example.invalid:443",
            "tuic://$id:p@example.invalid:443?allow_insecure=true",
            "mierus://u:p@example.invalid?profile=a&port=443&port=444&protocol=TCP&protocol=UDP",
            "mierus://u:p@example.invalid?profile=a&port=443&protocol=TCP&mtu=999",
            "mierus://u:p@example.invalid?profile=a&port=443&protocol=UNKNOWN"
        )) assertTrue(runCatching { CoreLink.parse(link) }.isFailure)
    }
    @Test fun dnsPathsAreDeferredAndPreviousMeasurementsCannotCountAsCurrentSuccess() {
        for (kind in listOf(TunnelType.SOAR, TunnelType.SOAR_SSH, TunnelType.DNSTT, TunnelType.NOIZDNS, TunnelType.VAYDNS, TunnelType.SLIPSTREAM)) {
            val profile = ServerProfile(name="DNS fixture", tunnelType=kind)
            assertTrue(ProfileCollections.isDns(profile))
            val previous = PingResult.Success(50, verified=true)
            val historical = ProbeCache.historical(mapOf(1L to previous))[1L]!!
            assertFalse(ProbeCache.fresh(historical))
            assertEquals(previous.measuredAt, (historical as PingResult.Success).measuredAt)
        }
        assertEquals(CollectionPhase.Completion.COMPLETE, CollectionPhase.completion(true,false,false,true,true))
        assertEquals(CollectionPhase.Completion.OFFER_DNS, CollectionPhase.completion(false,false,false,true))
        assertEquals(CollectionPhase.Completion.INCOMPLETE, CollectionPhase.completion(false,true,false,true))
    }
    @Test fun advancedFormsCreateSupportedSettingsFromScratch() {
        val ss=CoreForm.fresh("ss").with("server","example.invalid").with("auth", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=").with("method","2022-blake3-aes-256-gcm")
        assertEquals("shadowsocks",CoreLink.parse(ss.encode("SS2022")).outbound.getString("protocol"))
        val hy2=CoreForm.fresh("hysteria2").with("server","example.invalid").with("auth","fixture").with("mport","443,8443").with("hop-interval","20")
        assertTrue(CoreLink.parse(hy2.encode("HY2")).outbound.toString().contains("udphop"))
        val wg=CoreForm.fresh("wireguard").with("privatekey","AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=").with("publickey","AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")
            .with("address","10.77.0.2/32").with("endpoint","example.invalid:51820").with("jc","4").with("jmin","40").with("jmax","100").with("s1","32").with("s2","48").with("h1","1000").with("h2","2000").with("h3","3000").with("h4","4000")
        assertEquals("amneziawg",CoreLink.parse(wg.encode("AWG")).outbound.getString("protocol"))
    }
}
