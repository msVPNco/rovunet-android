package app.slipnet.service

import app.slipnet.domain.model.*
import org.junit.Assert.*
import org.junit.Test

class VpnPowerPolicyTest {
    @Test fun idleTcpSleepsButDnsAndExplicitCoverTrafficKeepTheirTiming() {
        val tcp = ServerProfile(name = "fixture", domain = "example.invalid", tunnelType = TunnelType.VLESS)
        assertFalse(VpnPowerPolicy.continuous(tcp))
        assertFalse(VpnPowerPolicy.holdCpu(false, false, 60_000))
        assertTrue(VpnPowerPolicy.holdCpu(false, true, 600_000))
        assertTrue(VpnPowerPolicy.holdCpu(false, false, 59_999))
        for (type in listOf(TunnelType.SOAR, TunnelType.DNSTT, TunnelType.NOIZDNS_SSH, TunnelType.SLIPSTREAM)) {
            assertTrue(VpnPowerPolicy.continuous(tcp.copy(tunnelType = type)))
            assertTrue(VpnPowerPolicy.holdCpu(true, false, 600_000))
        }
        assertTrue(VpnPowerPolicy.continuous(tcp.copy(wsPaddingEnabled = true)))
    }
    @Test fun screenOffSamplingBacksOffAndVisibleSamplingRecovers() {
        assertEquals(10_000L, VpnPowerPolicy.sampleInterval(false, 0))
        assertEquals(30_000L, VpnPowerPolicy.sampleInterval(false, 3))
        assertEquals(60_000L, VpnPowerPolicy.sampleInterval(false, 6))
        assertEquals(1000L, VpnPowerPolicy.sampleInterval(true, 0))
    }
}
