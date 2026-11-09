package app.slipnet.domain

import app.slipnet.domain.model.SmartPathPolicy
import org.junit.Assert.*
import org.junit.Test

class SmartPathPolicyTest {
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
