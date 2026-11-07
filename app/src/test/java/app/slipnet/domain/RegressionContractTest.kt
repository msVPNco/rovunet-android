package app.slipnet.domain

import org.junit.Test
import org.junit.Assert.*
import app.slipnet.domain.model.SoarConfig
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
            app.slipnet.presentation.main.dataPathNoteVisible(state, fresh) }
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

    @Test fun explicitChoiceAndHistoricalEvidence() = connectionChoiceRegression()
    @Test fun phaseBudgetsCancellationAndApprovalWait() = collectionPhaseRegression()
    @Test fun attemptExpirationCancellationAndReplacement() = attemptWatchdogRegression()
}
