package app.slipnet.presentation.scanner

import android.content.res.Configuration
import android.os.SystemClock
import androidx.compose.foundation.layout.Column
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.unit.LayoutDirection
import androidx.test.platform.app.InstrumentationRegistry
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModelStore
import app.slipnet.R
import app.slipnet.domain.model.*
import app.slipnet.presentation.main.ConnectionSelectionGuide
import app.slipnet.presentation.theme.SlipstreamTheme
import app.slipnet.tunnel.SoarBridge
import app.slipnet.tunnel.SoarResolverProbe
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.MutableStateFlow
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import java.net.ServerSocket
import java.util.Locale

class SoarScannerDeviceTest {
    @get:Rule val compose = createComposeRule()
    private val fixture get() = JSONObject(String(android.util.Base64.decode(InstrumentationRegistry.getArguments().getString("soarFixture")
        ?: error("A real loopback Soar peer is required"), android.util.Base64.DEFAULT), Charsets.UTF_8))
    private val host = "10.0.2.2"
    private fun profile() = ServerProfile(id = 909, name = "synthetic", tunnelType = TunnelType.SOAR,
        domain = fixture.getString("zone"), dnsttPublicKey = fixture.getString("publicKey"))

    @Test fun oldProtocolProofIsDiscardedWithoutLosingImportedResolverCandidates() = runBlocking {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val context = instrumentation.targetContext
        val preferences = app.slipnet.data.local.datastore.PreferencesDataStore(context)
        val previous = preferences.getSavedScanSession()
        val candidates = listOf("192.0.2.10", "192.0.2.11")
        val selected = profile()
        val profiles = java.lang.reflect.Proxy.newProxyInstance(javaClass.classLoader,
            arrayOf(app.slipnet.domain.repository.ProfileRepository::class.java)) { _, method, _ ->
            when (method.name) { "getProfileById" -> selected; else -> error("Unexpected profile operation: ${method.name}") }
        } as app.slipnet.domain.repository.ProfileRepository
        val scanner = java.lang.reflect.Proxy.newProxyInstance(javaClass.classLoader,
            arrayOf(app.slipnet.domain.repository.ResolverScannerRepository::class.java)) { _, method, _ ->
            when (method.name) {
                "getDefaultResolvers" -> listOf("198.51.100.1")
                "loadCountryCidrRanges" -> emptyList<Pair<Long, Long>>()
                else -> error("A cache restore must not start a network scan: ${method.name}")
            }
        } as app.slipnet.domain.repository.ResolverScannerRepository
        val vpn = object : app.slipnet.domain.repository.VpnRepository {
            override val connectionState = MutableStateFlow<ConnectionState>(ConnectionState.Disconnected)
            override val trafficStats = MutableStateFlow(TrafficStats.EMPTY)
            override suspend fun connect(profile: ServerProfile): Result<Unit> = error("Unexpected connection")
            override suspend fun disconnect(): Result<Unit> = error("Unexpected disconnect")
            override fun isConnected() = false
            override fun getConnectedProfile(): ServerProfile? = null
        }
        try {
            for (identity in listOf(null, "a-different-profile-and-protocol")) {
                val cache = mutableMapOf<String, Any>("resolverList" to candidates, "testDomain" to "old.invalid",
                    "timeoutMs" to "3000", "concurrency" to "50", "listSource" to "IMPORTED", "scanMode" to "PRISM",
                    "scanTransport" to "UDP", "scannedCount" to 2, "workingCount" to 1,
                    "results" to listOf(mapOf("host" to candidates[0], "status" to "WORKING", "e2eSuccess" to true, "prismVerified" to true)))
                if (identity != null) cache["scanIdentity"] = identity
                preferences.saveScanSession(com.google.gson.Gson().toJson(cache))
                app.slipnet.service.ScanStateHolder.begin()
                val store = ViewModelStore()
                lateinit var vm: DnsScannerViewModel
                try {
                    instrumentation.runOnMainSync {
                        vm = DnsScannerViewModel(SavedStateHandle(mapOf("profileId" to selected.id)), scanner, profiles, preferences, vpn, context)
                        store.put("scanner", vm)
                    }
                    val state = withTimeout(5000) { vm.uiState.first { it.resolverList == candidates && it.testDomain == selected.domain } }
                    assertEquals(ListSource.IMPORTED, state.listSource)
                    assertTrue(state.scannerState.results.isEmpty())
                    assertTrue(state.hostTransportSupport.isEmpty())
                    assertEquals(0, state.simpleModeE2eState.testedCount)
                    assertEquals(ScanMode.SIMPLE, state.scanMode)
                    assertEquals(ScanTransportMode.BOTH, state.scanTransport)
                } finally { instrumentation.runOnMainSync { store.clear() } }
            }
        } finally {
            if (previous == null) preferences.clearScanSession() else preferences.saveScanSession(previous)
        }
    }

    @Test fun soarTransportDefaultsToBothAndDoesNotChangeTheOtherScannerPreference() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val preferences = app.slipnet.data.local.datastore.PreferencesDataStore(context)
        val old = preferences.soarScannerTransport.first()
        val other = preferences.scannerTransport.first()
        assertEquals("BOTH", old)
        try {
            preferences.setSoarScannerTransport("TCP")
            assertEquals("TCP", preferences.soarScannerTransport.first())
            assertEquals(other, preferences.scannerTransport.first())
        } finally { preferences.setSoarScannerTransport(old) }
    }

    @Test fun fourSoarModesAndConnectionGuideUsePersianResources() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val configuration = Configuration(context.resources.configuration).apply { setLocale(Locale("fa")); setLayoutDirection(Locale("fa")) }
        val resources = context.createConfigurationContext(configuration).resources
        val state = DnsScannerUiState(profileId = 909, profile = profile(), scanMode = ScanMode.ADVANCED)
        assertTrue(state.canUseSimpleMode)
        assertTrue(state.canUsePrismMode) // A base64 Ed25519 key, not a hex Noise key.
        assertTrue(state.e2eSupported)
        val mode = mutableStateOf(ScanMode.SIMPLE)
        compose.setContent {
            CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides configuration,
                LocalLayoutDirection provides LayoutDirection.Rtl) {
                SlipstreamTheme {
                    Column {
                        ScanModeToggle(mode.value, true, true, isSoarProfile = true, enabled = true) { mode.value = it }
                        ConnectionSelectionGuide()
                    }
                }
            }
        }
        for ((value, key) in listOf(ScanMode.SIMPLE to R.string.scanner_simple, ScanMode.ADVANCED to R.string.rv_advanced_9f088d,
                ScanMode.E2E to R.string.scanner_e2e, ScanMode.PRISM to 0)) {
            compose.onNodeWithText(if (key == 0) "Prism" else resources.getString(key)).performClick()
            compose.runOnIdle { assertEquals(value, mode.value) }
        }
        compose.onNodeWithText(resources.getString(R.string.connection_selection_guide), substring = true).assertExists()
        compose.onAllNodesWithText("Start with a profile", substring = true).assertCountEquals(0)
    }

    @Test fun tcpOnlyResolverPassesWhileUdpAndWrongServerKeyFail() = runBlocking {
        val p = profile(); val port = fixture.getInt("tcpOnlyPort")
        assertFalse(SoarResolverProbe.test(host, port, p, DnsTransport.UDP, 800, "", false).success)
        assertTrue(SoarResolverProbe.test(host, port, p, DnsTransport.TCP, 6000, "", false).success)
        val wrong = p.copy(dnsttPublicKey = "00".repeat(32))
        assertFalse(SoarResolverProbe.test(host, port, wrong, DnsTransport.TCP, 1000, "", false).success)
    }

    @Test fun prismRepeatsRealSoarHandshakesAndRejectsOrdinaryDnsReplies() = runBlocking {
        val verified = SoarResolverProbe.verifyDetailed(host, fixture.getInt("tcpOnlyPort"), profile(), DnsTransport.TCP, 8000, 5, 2)
        assertEquals(2, verified.passed)
        assertEquals("Five configured probes must stop after two proven handshakes", 2, verified.attempted)
        assertEquals(0, SoarResolverProbe.verify(host, fixture.getInt("fakeTcpPort"), profile(), DnsTransport.TCP, 1000, 1, 1))
        assertTrue(SoarResolverProbe.test(host, fixture.getInt("udpPort"), profile().copy(dnsttPublicKey = fixture.getString("publicKeyHex")),
            DnsTransport.UDP, 6000, "", false).success)
    }

    @Test fun prismPublishesUdpBeforeTcpAndKeepsTheFirstProofTime() = runBlocking {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val context = instrumentation.targetContext
        val preferences = app.slipnet.data.local.datastore.PreferencesDataStore(context)
        val previous = preferences.getSavedScanSession()
        val resolver = "192.0.2.44"
        val selected = profile()
        val tcp = CompletableDeferred<Unit>()
        val scanner = object : app.slipnet.domain.repository.ResolverScannerRepository by app.slipnet.data.repository.ResolverScannerRepositoryImpl(context) {
            override fun getDefaultResolvers() = listOf(resolver)
            override suspend fun verifySoarResolverDetailed(host: String, port: Int, profile: ServerProfile,
                timeoutMs: Long, probeCount: Int, threshold: Int, transport: DnsTransport): ProbeVerification {
                if (transport == DnsTransport.UDP) { delay(50); return ProbeVerification(threshold, threshold) }
                tcp.await(); return ProbeVerification(0, 1)
            }
        }
        val profiles = java.lang.reflect.Proxy.newProxyInstance(javaClass.classLoader,
            arrayOf(app.slipnet.domain.repository.ProfileRepository::class.java)) { _, method, _ ->
            when (method.name) { "getProfileById" -> selected; else -> error("Unexpected profile operation: ${method.name}") }
        } as app.slipnet.domain.repository.ProfileRepository
        val vpn = object : app.slipnet.domain.repository.VpnRepository {
            override val connectionState = MutableStateFlow<ConnectionState>(ConnectionState.Disconnected)
            override val trafficStats = MutableStateFlow(TrafficStats.EMPTY)
            override suspend fun connect(profile: ServerProfile): Result<Unit> = error("A scan must not connect the VPN")
            override suspend fun disconnect(): Result<Unit> = error("A scan must not disconnect the VPN")
            override fun isConnected() = false
            override fun getConnectedProfile(): ServerProfile? = null
        }
        val store = ViewModelStore()
        lateinit var vm: DnsScannerViewModel
        var created = false
        try {
            preferences.saveScanSession(com.google.gson.Gson().toJson(mapOf("resolverList" to listOf(resolver),
                "testDomain" to "old.invalid", "listSource" to "IMPORTED", "scanMode" to "PRISM",
                "timeoutMs" to "3000", "concurrency" to "2", "scanTransport" to "BOTH")))
            app.slipnet.service.ScanStateHolder.begin()
            instrumentation.runOnMainSync {
                vm = DnsScannerViewModel(SavedStateHandle(mapOf("profileId" to selected.id)), scanner, profiles, preferences, vpn, context)
                created = true
                store.put("progress", vm)
            }
            withTimeout(5000) { vm.uiState.first { it.profile != null && it.resolverList == listOf(resolver) && it.testDomain == selected.domain } }
            instrumentation.runOnMainSync { vm.setScanMode(ScanMode.PRISM); vm.startScan() }
            val first = withTimeout(5000) { vm.uiState.first { it.scannerState.results.singleOrNull()?.prismVerified == true } }
            assertEquals(1, first.scannerState.scannedCount)
            assertEquals(1, first.scannerState.workingCount)
            assertEquals(true, first.hostTransportSupport[resolver]?.first)
            assertNull(first.hostTransportSupport[resolver]?.second)
            assertTrue(first.scannerState.isScanning)
            delay(250)
            tcp.complete(Unit)
            val complete = withTimeout(5000) { vm.uiState.first { !it.scannerState.isScanning && it.hostTransportSupport[resolver]?.second == false } }
            assertEquals(first.scannerState.results.single().responseTimeMs, complete.scannerState.results.single().responseTimeMs)
            assertEquals(1, complete.scannerState.scannedCount)
            assertEquals(1, complete.scannerState.workingCount)
        } finally {
            tcp.complete(Unit)
            instrumentation.runOnMainSync { if (created) vm.stopAll(); store.clear() }
            if (previous == null) preferences.clearScanSession() else preferences.saveScanSession(previous)
        }
    }

    @Test fun fullHttpAndCancellationLeaveTheExistingSoarClientUntouched() = runBlocking {
        val p = profile(); val port = fixture.getInt("tcpOnlyPort")
        val listen = ServerSocket(0).use { it.localPort }
        assertTrue(SoarBridge.startClient("tcp://$host:$port", p.domain, p.dnsttPublicKey, listen, tcpMode = "only").isSuccess)
        try {
            val checked = SoarResolverProbe.test(host, port, p, DnsTransport.TCP, 8000, "http://fixture.invalid/generate_204", true)
            assertTrue(checked.success); assertEquals(204, checked.httpStatusCode)
            val pending = launch { SoarResolverProbe.test(host, port, p, DnsTransport.UDP, 15000, "", false) }
            delay(200)
            val stopped = SystemClock.elapsedRealtime(); pending.cancelAndJoin()
            assertTrue("Soar scan cancellation must close its own client promptly", SystemClock.elapsedRealtime() - stopped < 2000)
            assertEquals(listen, SoarBridge.getClientPort())
        } finally { SoarBridge.stopClient() }
    }
}
