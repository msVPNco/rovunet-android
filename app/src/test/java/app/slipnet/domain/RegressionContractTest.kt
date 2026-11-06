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

    @Test fun explicitChoiceAndHistoricalEvidence() = connectionChoiceRegression()
    @Test fun phaseBudgetsCancellationAndApprovalWait() = collectionPhaseRegression()
    @Test fun attemptExpirationCancellationAndReplacement() = attemptWatchdogRegression()
}
