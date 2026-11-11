package app.slipnet.domain

import app.slipnet.domain.model.SmartPathPolicy
import org.junit.Assert.*
import org.junit.Test

class SmartPathPolicyTest {
    @Test fun wireguardCandidatesCannotRoamTheActiveWireguardPeerDuringIndependentChecks() {
        val uri = app.slipnet.domain.model.ServerProfile(name = "Synthetic peer", tunnelType = app.slipnet.domain.model.TunnelType.XRAY, coreLink = "wireguard://synthetic@example.invalid:51821")
        val file = uri.copy(coreLink = "[Interface]\nPrivateKey = synthetic")
        val reality = uri.copy(coreLink = "vless://synthetic@example.invalid:443?security=reality")
        assertFalse(SmartPathPolicy.independentCandidatesCompatible(uri, file))
        assertFalse(SmartPathPolicy.independentCandidatesCompatible(file, uri))
        assertTrue(SmartPathPolicy.independentCandidatesCompatible(reality, uri))
        assertTrue(SmartPathPolicy.independentCandidatesCompatible(uri, reality))
    }
    @Test fun ownerExamplesRequireBothSlowCurrentAndHalfTheDelay() {
        assertTrue(SmartPathPolicy.better(4000, 1500, 120_000))
        assertTrue(SmartPathPolicy.better(4000, 2000, 120_000))
        assertFalse(SmartPathPolicy.better(4000, 2500, 120_000))
        assertFalse(SmartPathPolicy.better(300, 100, 120_000))
        assertFalse(SmartPathPolicy.better(4000, 1000, 119_999))
        assertFalse(SmartPathPolicy.better(4000, 0, 120_000))
    }
    @Test fun threeRealSamplesRejectMissingEvidenceAndResistOneSpike() {
        assertNull(SmartPathPolicy.median(listOf(100, 110)))
        assertNull(SmartPathPolicy.median(listOf(0, 110, 120)))
        assertEquals(110L, SmartPathPolicy.median(listOf(100, 9000, 110)))
    }
}
