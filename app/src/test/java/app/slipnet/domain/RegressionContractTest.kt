package app.slipnet.domain

import org.junit.Test
import org.junit.Assert.*
import app.slipnet.domain.model.SoarConfig
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.ProfileCollections
import app.slipnet.domain.model.ProbePolicy
import app.slipnet.domain.model.ProfileDuplicates
import app.slipnet.domain.model.TunnelType

/** The command-line harness and Gradle execute the same portable contracts. */
class RegressionContractTest {
    @Test fun bothFreshSoarVariantsGetParallelDnsUnlessTheUserHasChosenAMode() {
        assertEquals(SoarConfig.TCP_ON, SoarConfig.newProfileTcpMode(TunnelType.SOAR_SSH))
        assertEquals(SoarConfig.TCP_ON, SoarConfig.newProfileTcpMode(TunnelType.SOAR))
        assertEquals(SoarConfig.TCP_AUTO, SoarConfig.newProfileTcpMode(TunnelType.DNSTT))
        for (mode in SoarConfig.TCP_MODES) {
            for (type in listOf(TunnelType.SOAR, TunnelType.SOAR_SSH)) {
                assertEquals(mode, SoarConfig.tcpModeAfterSshChange(mode, type, isNew = false, explicitlyChosen = false))
                assertEquals(mode, SoarConfig.tcpModeAfterSshChange(mode, type, isNew = true, explicitlyChosen = true))
            }
            assertEquals(mode, SoarConfig.tcpModeAfterSshChange(mode, TunnelType.DNSTT_SSH, isNew = true, explicitlyChosen = false))
        }
        assertEquals(SoarConfig.TCP_ON, SoarConfig.tcpModeAfterSshChange(SoarConfig.TCP_AUTO,
            TunnelType.SOAR_SSH, isNew = true, explicitlyChosen = false))
        assertEquals(SoarConfig.TCP_ON, SoarConfig.tcpModeAfterSshChange(SoarConfig.TCP_ON,
            TunnelType.SOAR, isNew = true, explicitlyChosen = false))
        assertEquals(SoarConfig.TCP_ON, SoarConfig.tcpModeAfterSshChange(SoarConfig.TCP_AUTO,
            TunnelType.SOAR, isNew = true, explicitlyChosen = false))
    }

    @Test fun scanResultsReachSoarThroughItsTcpPolicy() {
        // TCP-only scan or mixed result: both paths, never loosening a TCP-only policy.
        for (hint in listOf("TCP_SCAN", "MIXED")) {
            assertEquals(SoarConfig.TCP_ON, SoarConfig.tcpModeAfterScan(SoarConfig.TCP_AUTO, hint))
            assertEquals(SoarConfig.TCP_ON, SoarConfig.tcpModeAfterScan(SoarConfig.TCP_OFF, hint))
            assertEquals(SoarConfig.TCP_ON, SoarConfig.tcpModeAfterScan(SoarConfig.TCP_ON, hint))
            assertEquals(SoarConfig.TCP_ONLY, SoarConfig.tcpModeAfterScan(SoarConfig.TCP_ONLY, hint))
        }
        // Proven on TCP but not UDP.
        assertEquals(SoarConfig.TCP_ONLY, SoarConfig.tcpModeAfterScan(SoarConfig.TCP_AUTO, "TCP"))
        // UDP result or unknown hint: the user's policy stays.
        for (mode in SoarConfig.TCP_MODES) {
            assertEquals(mode, SoarConfig.tcpModeAfterScan(mode, "UDP"))
            assertEquals(mode, SoarConfig.tcpModeAfterScan(mode, "unknown"))
        }
    }

    @Test fun homeScreenHttpsNoteIsShortLivedAndNeverClaimsAnOldPass() {
        val shown = { state: app.slipnet.domain.model.DataPathState, fresh: Boolean ->
            app.slipnet.domain.model.dataPathNoteVisible(state, fresh) }
        val states = app.slipnet.domain.model.DataPathState.values()
        // A pass is visible only while it is fresh; a failed check stays with its retry.
        assertTrue(shown(app.slipnet.domain.model.DataPathState.VERIFIED, true))
        assertFalse(shown(app.slipnet.domain.model.DataPathState.VERIFIED, false))
        assertTrue(shown(app.slipnet.domain.model.DataPathState.UNKNOWN, false))
        // Nothing is reserved on screen while untested or checking, or after a pass has aged.
        for (state in states.filter { it == app.slipnet.domain.model.DataPathState.UNTESTED || it == app.slipnet.domain.model.DataPathState.CHECKING }) {
            assertFalse(shown(state, true)); assertFalse(shown(state, false))
        }
    }

    @Test fun collectionTestsReachUdpCoresAndTcpChecksSkipThem() {
        val base = ServerProfile(id = 1, name = "x", tunnelType = TunnelType.XRAY)
        val reality = base.copy(coreLink = "vless://id@203.0.113.7:2083?security=reality&type=tcp#a b")
        val xhttp = base.copy(coreLink = "vless://id@example.org:2096?type=xhttp")
        val v6 = base.copy(coreLink = "vless://id@[2001:db8::1]:443?type=tcp")
        val hy2 = base.copy(coreLink = "hysteria2://pw@example.org:8443?sni=example.org")
        val wg = base.copy(coreLink = "[Interface]\nPrivateKey = x\n[Peer]\nEndpoint = example.org:51820")
        assertEquals("203.0.113.7" to 2083, ProfileCollections.coreTcpTarget(reality))
        assertEquals("example.org" to 2096, ProfileCollections.coreTcpTarget(xhttp))
        assertEquals("2001:db8::1" to 443, ProfileCollections.coreTcpTarget(v6))
        // A TCP connect says nothing about a UDP protocol, so there is no target to probe.
        assertNull(ProfileCollections.coreTcpTarget(hy2))
        assertNull(ProfileCollections.coreTcpTarget(wg))
        assertNull(ProfileCollections.coreTcpTarget(base.copy(coreLink = "vless://id@host?type=tcp")))
        // The all-paths budget grows with the collection, so the last-ordered UDP cores are reached.
        val seventeen = List(17) { reality.copy(id = it + 1L) }
        assertEquals(17 * 40_000L, ProbePolicy.testAllBudgetMs(seventeen))
        assertEquals(90_000L, ProbePolicy.testAllBudgetMs(listOf(reality)))
        assertEquals(900_000L, ProbePolicy.testAllBudgetMs(List(60) { reality.copy(id = it + 1L) }))
    }

    @Test fun importingTheSameListAgainAddsNothing() {
        val a = ServerProfile(id = 1, name = "a", domain = "one.example", collectionId = "c1", collectionName = "old")
        val b = ServerProfile(id = 2, name = "b", domain = "two.example", collectionId = "c1", collectionName = "old")
        val again = listOf(a.copy(id = 0, name = "renamed", collectionId = "n"), b.copy(id = 0, collectionId = "n"))
        assertTrue(ProfileDuplicates.withoutExisting(listOf(a, b), again).isEmpty())
        // A changed connection field is a different profile and is kept.
        val moved = a.copy(id = 0, domain = "three.example", collectionId = "n")
        assertEquals(listOf(moved), ProfileDuplicates.withoutExisting(listOf(a, b), listOf(again[0], moved)))
        // Nothing existing: everything is kept, in order.
        assertEquals(again, ProfileDuplicates.withoutExisting(emptyList(), again))
    }

    @Test fun explicitChoiceAndHistoricalEvidence() = connectionChoiceRegression()
    @Test fun phaseBudgetsCancellationAndApprovalWait() = collectionPhaseRegression()
    @Test fun attemptExpirationCancellationAndReplacement() = attemptWatchdogRegression()
}
