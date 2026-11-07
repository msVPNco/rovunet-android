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
        val verified = app.slipnet.domain.model.DataPathState.VERIFIED
        val deadline = 4_000L // The check passed at elapsed time 1,000.
        assertTrue(app.slipnet.domain.model.dataPathNoteVisible(verified, deadline, 1_000L))
        assertTrue(app.slipnet.domain.model.dataPathNoteVisible(verified, deadline, 3_999L))
        // Re-entering the page or rotating the phone does not create a new deadline.
        for (now in listOf(4_000L, 4_001L, 50_000L)) {
            assertFalse(app.slipnet.domain.model.dataPathNoteVisible(verified, deadline, now))
        }
        // A reset session has no deadline. A failed/background check never reserves a home row.
        assertFalse(app.slipnet.domain.model.dataPathNoteVisible(verified, 0L, 1_000L))
        for (state in app.slipnet.domain.model.DataPathState.values().filter { it != verified }) {
            assertFalse(app.slipnet.domain.model.dataPathNoteVisible(state, deadline, 1_000L))
        }
    }

    @Test fun unfinishedCollectionKeepsItsSpecificOutcomeAfterTimeout() = kotlinx.coroutines.runBlocking {
        val profiles = List(3) { ServerProfile(id = it + 1L, name = "synthetic", tunnelType = TunnelType.XRAY) }
        val results = profiles.associate { it.id to app.slipnet.domain.model.PingResult.Pending }.toMutableMap<Long, app.slipnet.domain.model.PingResult>()
        val passed = app.slipnet.domain.model.CollectionPhase.run(profiles, false, 200L,
            onProgress = { _, _ -> }, onResult = { p, r -> results[p.id] = r }, probe = {
                if (it.id == 1L) app.slipnet.domain.model.PingResult.Skipped
                else { kotlinx.coroutines.delay(10_000L); app.slipnet.domain.model.PingResult.Skipped }
            })
        val unfinished = results.values.any { it is app.slipnet.domain.model.PingResult.Pending }
        assertFalse(passed)
        assertTrue(unfinished)
        for (hasDns in listOf(false, true)) {
            assertEquals(app.slipnet.domain.model.CollectionPhase.Completion.INCOMPLETE,
                app.slipnet.domain.model.CollectionPhase.completion(passed, unfinished, false, hasDns))
        }
        assertEquals(app.slipnet.domain.model.CollectionPhase.Completion.NEEDS_REVIEW,
            app.slipnet.domain.model.CollectionPhase.completion(false, false, true, true))
    }

    @Test fun everyConnectionStateHasItsOwnIndicator() {
        val profile = ServerProfile(name = "synthetic")
        val states = listOf(app.slipnet.domain.model.ConnectionState.Disconnected,
            app.slipnet.domain.model.ConnectionState.Connecting, app.slipnet.domain.model.ConnectionState.Connected(profile),
            app.slipnet.domain.model.ConnectionState.Disconnecting, app.slipnet.domain.model.ConnectionState.Error("synthetic"))
        assertEquals(5, states.map { it.indicator }.toSet().size)
    }

    @Test fun lockedImportDuplicatesAreSkippedWithoutChangingRestrictions() {
        val locked = ServerProfile(name = "synthetic", domain = "example.invalid", isLocked = true,
            lockPasswordHash = "synthetic-hash", expirationDate = 4_000_000_000_000L,
            allowSharing = false, boundDeviceId = "synthetic-device")
        val renamed = locked.copy(id = 9, name = "other", collectionId = "other")
        assertTrue(ProfileDuplicates.withoutExisting(listOf(locked), listOf(renamed)).isEmpty())
        assertEquals(listOf(locked), ProfileDuplicates.withinImport(listOf(locked, renamed)))
        assertEquals(listOf(locked), ProfileDuplicates.withoutExisting(emptyList(), listOf(locked, renamed)))
        for (changed in listOf(locked.copy(lockPasswordHash = "other-hash"),
                locked.copy(expirationDate = locked.expirationDate + 1), locked.copy(boundDeviceId = "other-device"),
                locked.copy(allowSharing = true), locked.copy(isLocked = false), locked.copy(sshPassword = "other-password"))) {
            assertEquals(listOf(changed), ProfileDuplicates.withoutExisting(listOf(locked), listOf(changed)))
        }
        // Unreadable stored credentials are not evidence that the full incoming config is present.
        assertEquals(listOf(locked), ProfileDuplicates.withoutExisting(
            listOf(locked.copy(unreadableEnvelopeToken = "unreadable")), listOf(locked)))
        assertTrue(ProfileDuplicates.plan(listOf(locked.copy(id = 1), renamed)).isEmpty())
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
    @Test fun scannerHttpUrlUsesTheExplicitSocksTunnelWithoutLocalDns() = kotlinx.coroutines.runBlocking {
        java.net.ServerSocket(0).use { server ->
            val observed = java.util.concurrent.CompletableFuture<String>()
            val peer = Thread {
                try {
                    server.accept().use { socket ->
                        socket.soTimeout = 3000
                        val input = java.io.DataInputStream(socket.getInputStream())
                        val out = socket.getOutputStream()
                        check(input.readUnsignedByte() == 5)
                        input.readFully(ByteArray(input.readUnsignedByte()))
                        out.write(byteArrayOf(5, 0)); out.flush()
                        check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 1 && input.readUnsignedByte() == 0)
                        check(input.readUnsignedByte() == 3)
                        val domain = ByteArray(input.readUnsignedByte()); input.readFully(domain)
                        check(input.readUnsignedShort() == 80)
                        out.write(byteArrayOf(5, 0, 0, 1, 127, 0, 0, 1, 0, 0)); out.flush()
                        val request = socket.getInputStream().bufferedReader().readLine()
                        observed.complete(String(domain) + " " + request)
                        out.write("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\n\r\n".toByteArray()); out.flush()
                    }
                } catch (e: Throwable) { observed.completeExceptionally(e) }
            }.apply { isDaemon = true; start() }
            assertTrue(app.slipnet.core.TunnelProbe.measureUrlCancellable(server.localPort,
                "http://never-resolve.invalid/probe?x=1", 2000) >= 0)
            assertEquals("never-resolve.invalid GET /probe?x=1 HTTP/1.1", observed.get(3, java.util.concurrent.TimeUnit.SECONDS))
            peer.join(3000)
        }
    }
    @Test fun phaseBudgetsCancellationAndApprovalWait() = collectionPhaseRegression()
    @Test fun attemptExpirationCancellationAndReplacement() = attemptWatchdogRegression()
}
