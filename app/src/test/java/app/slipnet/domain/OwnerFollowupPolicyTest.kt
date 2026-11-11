package app.slipnet.domain

import app.slipnet.domain.model.*
import org.junit.Assert.*
import org.junit.Test

class OwnerFollowupPolicyTest {
    private val radios = listOf("wifi" to setOf(1), "mobile" to setOf(0))
    @Test fun wholeCollectionNeverOffersDnsAfterVerifiedDirectSuccess() {
        assertEquals(CollectionPhase.Completion.COMPLETE, CollectionPhase.completion(true, false, false, true, includeDnsAfterSuccess = true))
        assertEquals(CollectionPhase.Completion.COMPLETE, CollectionPhase.completion(true, false, false, true))
        assertEquals(CollectionPhase.Completion.OFFER_DNS, CollectionPhase.completion(false, false, false, true))
        assertEquals(CollectionPhase.Completion.NEEDS_REVIEW, CollectionPhase.completion(false, false, true, true))
        assertEquals(CollectionPhase.Completion.INCOMPLETE, CollectionPhase.completion(true, true, false, true, includeDnsAfterSuccess = true))
    }
    @Test fun coreDiagnosticsClassifyWithoutRetainingSecretsOrCallingReadyAHandshake() {
        val f = app.slipnet.core.CoreRuntimeFailure
        assertEquals(app.slipnet.util.ConnectionDiagnostics.Failure.NONE, f.category("2026/11/10 [Warning] core: Xray 26.1.0 started"))
        assertEquals(app.slipnet.util.ConnectionDiagnostics.Failure.TIMEOUT, f.category("[Warning] upstream private.invalid:8443: i/o timeout"))
        assertEquals(app.slipnet.util.ConnectionDiagnostics.Failure.TLS, f.category("[Error] certificate validation failed for private.invalid"))
        assertEquals(app.slipnet.util.ConnectionDiagnostics.Failure.NONE, f.category("ordinary user-controlled text"))
    }
    @Test fun vpnCreationAndSecondaryRadioDoNotChangePhysicalRoute() {
        assertEquals("wifi", PhysicalRouteSelection.choose("wifi", false, emptyList(), 1, radios, null))
        assertEquals("wifi", PhysicalRouteSelection.choose("vpn", true, emptyList(), 1, radios, "wifi"))
        assertEquals("wifi", PhysicalRouteSelection.choose("vpn", true, emptyList(), null, radios, "wifi"))
        assertEquals("wifi", PhysicalRouteSelection.choose("vpn", true, listOf("wifi"), null, radios, "mobile"))
    }
    @Test fun trueHandoverAndOfflineAreNotHiddenByRememberedRoute() {
        assertEquals("mobile", PhysicalRouteSelection.choose("vpn", true, emptyList(), 0, radios, "wifi"))
        assertEquals("mobile", PhysicalRouteSelection.choose("mobile", false, emptyList(), null, radios, "wifi"))
        assertNull(PhysicalRouteSelection.choose("vpn", true, emptyList(), null, emptyList(), "wifi"))
        assertNull(PhysicalRouteSelection.choose("vpn", true, emptyList(), null, radios, null))
    }
    @Test fun oneSpikeAndRepeatedCachedSamplesDoNotTriggerDiscovery() {
        val h = LivePathHealth()
        h.add(100, "one", 0); h.add(9000, "one", 30_000)
        assertFalse(h.add(110, "one", 60_000).unstable)
        assertEquals(110L, h.state().medianMs)
        val failed = LivePathHealth()
        failed.add(null, "one", 0)
        assertEquals(1, failed.add(null, "one", 1000).failed)
        assertFalse(failed.add(null, "one", 30_000).unstable)
        assertTrue(failed.add(null, "one", 60_000).unstable)
    }
    @Test fun threeSlowCyclesTriggerButDestinationChangesAndIdleResetEvidence() {
        val h = LivePathHealth()
        h.add(4000, "one", 0); h.add(4100, "one", 30_000)
        assertTrue(h.add(3900, "one", 60_000).unstable)
        assertFalse(h.add(5000, "two", 90_000).unstable)
        h.add(5000, "two", 120_000)
        assertFalse(h.add(5000, "two", 400_000).unstable)
        assertFalse(SmartPathPolicy.better(4000, 1900, 119_999))
        assertTrue(SmartPathPolicy.better(4000, 1900, 120_000))
    }
}
