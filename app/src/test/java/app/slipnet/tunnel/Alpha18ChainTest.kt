package app.slipnet.tunnel

import app.slipnet.domain.model.*
import org.junit.Assert.*
import org.junit.Test

class Alpha18ChainTest {
    private val ssh = ServerProfile(name = "Inner SSH", tunnelType = TunnelType.SSH)
    private fun core(link: String) = ServerProfile(name = "Outer core", tunnelType = TunnelType.XRAY, coreLink = link)
    @Test fun reachableUdpOrTcpCoreCanProvideSocksForSsh() {
        for (link in listOf("hysteria2://synthetic@example.invalid:443", "wireguard://synthetic", "vless://synthetic@example.invalid:443")) {
            assertNull(ChainValidation.validate(listOf(core(link), ssh)))
            assertEquals(LayerOutput.SOCKS5, ChainValidation.outputType(TunnelType.XRAY))
        }
    }
    @Test fun sshCannotSilentlyPretendToCarryUdpIntoXray() {
        assertNotNull(ChainValidation.validate(listOf(ssh, core("hysteria2://synthetic@example.invalid"))))
        assertNotNull(ChainValidation.validate(listOf(core("synthetic"), ssh, ssh)))
        assertNotNull(ChainValidation.validate(listOf(core("synthetic"), core("synthetic"))))
        assertNotNull(ChainValidation.validate(listOf(core("synthetic"), ssh.copy(isLocked = false, expirationDate = 1))))
    }
}
