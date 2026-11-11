package app.slipnet.util

import org.junit.Assert.*
import org.junit.Test

class ConnectionDiagnosticsTest {
    @Test fun soarPolicyDescribesConfigurationWithoutClaimingReadinessOrAcceptingLateAttempts() {
        ConnectionDiagnostics.clear()
        ConnectionDiagnostics.begin(801, "SOAR_SSH")
        ConnectionDiagnostics.soarPolicy(801, ConnectionDiagnostics.SoarMode.UDP_AND_TCP,
            ConnectionDiagnostics.SoarMode.UDP_AND_TCP)
        ConnectionDiagnostics.begin(802, "CHAIN")
        ConnectionDiagnostics.soarPolicy(802, ConnectionDiagnostics.SoarMode.AUTO,
            ConnectionDiagnostics.SoarMode.TCP_ONLY)
        ConnectionDiagnostics.soarPolicy(801, ConnectionDiagnostics.SoarMode.UDP_ONLY,
            ConnectionDiagnostics.SoarMode.UDP_ONLY)
        val report = ConnectionDiagnostics.report()
        assertTrue(report.substringBefore("ATTEMPT 802").contains("SOAR_POLICY requested=UDP_AND_TCP configured=UDP_AND_TCP"))
        assertTrue(report.substringAfter("ATTEMPT 802").contains("SOAR_POLICY requested=AUTO configured=TCP_ONLY"))
        assertFalse(report.contains("UDP_ONLY"))
        assertFalse(report.contains("VERIFIED"))
        assertFalse(report.contains("VPN_ESTABLISHED"))
        assertEquals(ConnectionDiagnostics.SoarMode.UDP_AND_TCP, ConnectionDiagnostics.SoarMode.fromNative("on"))
        assertEquals(ConnectionDiagnostics.SoarMode.TCP_ONLY, ConnectionDiagnostics.SoarMode.fromNative("only"))
        assertEquals(ConnectionDiagnostics.SoarMode.UDP_ONLY, ConnectionDiagnostics.SoarMode.fromNative("off"))
        listOf("auto", "ON", "", " on ", "endpoint.invalid").forEach {
            assertEquals(ConnectionDiagnostics.SoarMode.AUTO, ConnectionDiagnostics.SoarMode.fromNative(it))
        }
    }

    @Test fun trafficSnapshotsBelongToTheirAttemptAndCannotInventSuccessOrOverwriteNewerCounters() {
        ConnectionDiagnostics.clear()
        ConnectionDiagnostics.begin(901, "SOAR")
        ConnectionDiagnostics.traffic(901, 4096, 8192)
        ConnectionDiagnostics.traffic(901, -1, 10)
        ConnectionDiagnostics.begin(902, "SOCKS5")
        ConnectionDiagnostics.traffic(901, 999999, 999999)
        ConnectionDiagnostics.traffic(902, 0, 0)
        val report = ConnectionDiagnostics.report()
        assertTrue(report.substringBefore("ATTEMPT 902").contains("COUNTERS sent=4096 received=8192"))
        assertTrue(report.substringAfter("ATTEMPT 902").contains("COUNTERS sent=0 received=0"))
        assertFalse(report.contains("999999"))
        assertFalse(report.contains("VERIFIED"))
    }

    @Test fun errorsAreClassifiedWithoutRetainingSecretsOrEndpoints() {
        ConnectionDiagnostics.clear()
        ConnectionDiagnostics.begin(101, "SSH")
        val error = java.io.IOException("Auth fail password=synthetic-secret 192.0.2.17:22 user=synthetic-user")
        ConnectionDiagnostics.record(ConnectionDiagnostics.Event.SSH_FAILED, 101, ConnectionDiagnostics.failure(error))
        val text = ConnectionDiagnostics.report()
        assertTrue(text.contains("AUTH_REJECTED"))
        listOf("synthetic-secret", "192.0.2.17", "synthetic-user", "password=").forEach { assertFalse(text.contains(it)) }
        ConnectionDiagnostics.begin(102, "an-arbitrary-server.example.invalid")
        assertFalse(ConnectionDiagnostics.report().contains("an-arbitrary-server"))
    }

    @Test fun busyTrafficDoesNotEvictTheConnectionAndStopTimeline() {
        ConnectionDiagnostics.clear()
        ConnectionDiagnostics.begin(201, "SOCKS5")
        repeat(5000) { ConnectionDiagnostics.flow(ConnectionDiagnostics.Event.DNS_CONNECT, 201, 2) }
        ConnectionDiagnostics.record(ConnectionDiagnostics.Event.DISCONNECT_REQUEST, 201)
        ConnectionDiagnostics.record(ConnectionDiagnostics.Event.STARTUP_IO_CLOSED, 201)
        val report = ConnectionDiagnostics.report()
        assertTrue(report.contains("CONNECT_REQUEST"))
        assertTrue(report.contains("DISCONNECT_REQUEST"))
        assertTrue(report.contains("STARTUP_IO_CLOSED"))
        assertTrue(report.contains("POLICY_DENIED count=5000"))
        assertTrue(report.length < 2000)
    }

    @Test fun lateOldEventsStayInTheOldAttemptAndCancellationIsNotATimeout() {
        ConnectionDiagnostics.clear()
        ConnectionDiagnostics.begin(301, "SOAR_SSH")
        ConnectionDiagnostics.begin(302, "SSH")
        ConnectionDiagnostics.record(ConnectionDiagnostics.Event.SSH_CANCELLED, 301, ConnectionDiagnostics.Failure.CANCELLED)
        val report = ConnectionDiagnostics.report()
        assertTrue(report.substringBefore("ATTEMPT 302").contains("SSH_CANCELLED"))
        assertFalse(report.substringAfter("ATTEMPT 302").contains("SSH_CANCELLED"))
        assertEquals(ConnectionDiagnostics.Failure.CANCELLED, ConnectionDiagnostics.failure(java.util.concurrent.CancellationException("synthetic")))
        assertEquals(ConnectionDiagnostics.Failure.TIMEOUT, ConnectionDiagnostics.failure(java.net.SocketTimeoutException("synthetic")))
    }

    @Test fun onlyEightRecentAttemptsAreRetained() {
        ConnectionDiagnostics.clear()
        for (id in 401L..420L) ConnectionDiagnostics.begin(id, "DNSTT")
        val report = ConnectionDiagnostics.report()
        assertFalse(report.contains("ATTEMPT 401 "))
        assertTrue(report.contains("ATTEMPT 420 "))
        assertEquals(8, Regex("ATTEMPT ").findAll(report).count())
    }
}
