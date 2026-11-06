package app.slipnet.presentation

import android.content.Intent
import androidx.activity.compose.setContent
import androidx.lifecycle.ViewModelProvider
import androidx.test.core.app.ActivityScenario
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.ConnectionHistoryStore
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.domain.model.*
import app.slipnet.domain.repository.ProfileRepository
import app.slipnet.presentation.main.MainViewModel
import app.slipnet.service.VpnConnectionManager
import app.slipnet.tunnel.TransportTrust
import java.io.DataInputStream
import java.net.ServerSocket
import java.util.UUID
import kotlin.concurrent.thread
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.first
import org.junit.Assert.*
import org.junit.Test

class ConnectionHistoryDeviceTest {
    private val instrumentation get() = InstrumentationRegistry.getInstrumentation()
    private val context get() = instrumentation.targetContext
    private lateinit var repository: ProfileRepository
    private lateinit var historyStore: ConnectionHistoryStore

    private suspend fun initializeDependencies() {
        openScreen { vm, _ -> repository = vm.profileRepository; historyStore = vm.connectionHistoryStore }
        withTimeout(10_000) { check(historyStore.flush()) }
    }

    private suspend fun openScreen(block: suspend (MainViewModel, VpnConnectionManager) -> Unit) {
        ActivityScenario.launch<MainActivity>(Intent(context, MainActivity::class.java)).use { scenario ->
            // Keep a single test-owned ViewModel while exercising its real Hilt dependencies and lifecycle.
            scenario.onActivity { it.setContent {} }
            instrumentation.waitForIdleSync()
            lateinit var vm: MainViewModel
            lateinit var manager: VpnConnectionManager
            scenario.onActivity { vm = ViewModelProvider(it)[MainViewModel::class.java]; manager = it.connectionManager }
            withTimeout(30_000) { vm.uiState.first { !it.isLoading } }
            block(vm, manager)
        }
    }

    private suspend fun onMain(action: () -> Unit) = withContext(Dispatchers.Main) { action() }

    @Test fun manualSshChoiceSurvivesCollectionChangesActivityReturnAndColdHistoryRead() = runBlocking<Unit> {
        initializeDependencies()
        val repo = repository
        val history = historyStore
        val preferences = PreferencesDataStore(context)
        val originalProxy = preferences.proxyOnlyMode.first()
        val port = requireNotNull(InstrumentationRegistry.getArguments().getString("sshFixturePort")).toInt()
        val group = "history-" + UUID.randomUUID()
        val ssh = ServerProfile(name = "Synthetic retained SSH", domain = "127.0.0.1", tunnelType = TunnelType.SSH,
            sshPort = port, sshUsername = "device-test", sshPassword = "synthetic-only", collectionId = group)
        val ids = repo.insertCollection(listOf(ssh, ssh.copy(name = "Synthetic alternate Naive", tunnelType = TunnelType.NAIVE),
            ssh.copy(name = "Other collection", collectionId = "$group-other")))
        val savedActive = repo.getActiveProfile().first()?.id
        try {
            preferences.setProxyOnlyMode(true)
            openScreen { vm, _ ->
                val selected = vm.uiState.value.profiles.single { it.id == ids[0] }
                val alternateGroup = vm.uiState.value.profiles.single { it.id == ids[2] }
                onMain { vm.tapProfile(selected); vm.tapCollection(alternateGroup); vm.tapCollection(selected) }
                assertEquals(ids[0], vm.uiState.value.manualProfileId)
                assertTrue(withTimeout(10_000) { history.flush() })
            }
            assertTrue(withTimeout(10_000) { history.flush() })
            val cold = ConnectionHistoryStore(preferences).load()
            val choices = ConnectionHistory.choices(cold, repo.getAllProfiles().first())
            assertEquals(ids[0], choices[group])
            openScreen { vm, manager ->
                assertEquals(ids[0], vm.uiState.value.manualProfileId)
                onMain { vm.connect() }
                try {
                    withTimeout(45_000) {
                        while (manager.connectionState.value !is ConnectionState.Connected) {
                            assertFalse("Remembered SSH failed before transport readiness", manager.connectionState.value is ConnectionState.Error)
                            TransportTrust.request.value?.let { TransportTrust.answer(it, true) }
                            delay(25)
                        }
                    }
                    assertEquals(ids[0], (manager.connectionState.value as ConnectionState.Connected).profile.id)
                } finally {
                    onMain { vm.disconnect() }
                    withTimeout(30_000) { manager.awaitSessionDisconnected(manager.currentSessionId) }
                }
            }
        } finally {
            ids.forEach { repo.deleteProfile(it) }
            if (savedActive != null) repo.setActiveProfile(savedActive) else repo.clearActiveProfile()
            preferences.setProxyOnlyMode(originalProxy)
        }
    }

    @Test fun completeGroupResultsSurviveProbeCleanupSqlSortAndActivityReturn() = runBlocking<Unit> {
        initializeDependencies()
        val repo = repository
        val history = historyStore
        val preferences = PreferencesDataStore(context)
        val originalProxy = preferences.proxyOnlyMode.first()
        val server = ServerSocket(0)
        val worker = thread(isDaemon = true, name = "history-test-rejecting-socks") {
            while (!server.isClosed) runCatching {
                val socket = server.accept()
                thread(isDaemon = true) { runCatching { socket.use {
                    it.soTimeout = 5_000
                    val input = DataInputStream(it.getInputStream())
                    if (input.readUnsignedByte() != 5) return@use
                    input.readFully(ByteArray(input.readUnsignedByte()))
                    it.getOutputStream().apply { write(byteArrayOf(5, 0)); flush() }
                    if (input.read() < 0) return@use
                    // A valid SOCKS reply explicitly refuses CONNECT; the app must retain this actual test result.
                    it.getOutputStream().apply { write(byteArrayOf(5, 5, 0, 1, 0, 0, 0, 0, 0, 0)); flush() }
                } } }
            }
        }
        val group = "probe-history-" + UUID.randomUUID()
        val base = ServerProfile(name = "Synthetic SOCKS path", domain = "127.0.0.1", tunnelType = TunnelType.SOCKS5,
            socks5ServerPort = server.localPort, collectionId = group)
        val ids = repo.insertCollection(listOf(base, base.copy(name = "Second synthetic SOCKS path")))
        val selectedBefore = repo.getActiveProfile().first()?.id
        val lastUserConnection = preferences.lastConnectedProfileId.first()
        try {
            preferences.setProxyOnlyMode(true)
            repo.setActiveProfile(ids[0])
            openScreen { vm, _ ->
                val profile = vm.uiState.value.profiles.single { it.id == ids[0] }
                onMain { vm.pingCollection(profile) }
                withTimeout(90_000) { vm.uiState.first { !it.isPingRunning && ids.all { id -> it.pingResults[id] is PingResult.Error } } }
                withTimeout(15_000) { vm.uiState.first { it.profiles.filter { p -> p.id in ids }.all { p -> p.deletionReview != null } } }
                assertTrue(ids.all { vm.uiState.value.pingResults[it] is PingResult.Error })
                assertEquals("Temporary probes changed the persistent selection", ids[0], repo.getActiveProfile().first()?.id)
                assertEquals("Temporary probes replaced the last user connection", lastUserConnection, preferences.lastConnectedProfileId.first())
                // A genuine mapper roundtrip changes both deletion-review digests. Results must still survive.
                repo.updateProfileOrder(ids.reversed())
                repo.updateLastConnectedAt(ids[1])
                withTimeout(15_000) { vm.uiState.first { state ->
                    state.profiles.filter { it.id in ids }.firstOrNull()?.id == ids[1] &&
                        state.profiles.any { it.id == ids[1] && it.lastConnectedAt > 0 }
                } }
                assertTrue(ids.all { vm.uiState.value.pingResults[it] is PingResult.Error })
                assertTrue(withTimeout(10_000) { history.flush() })
            }
            assertTrue(withTimeout(10_000) { history.flush() })
            val cold = ConnectionHistoryStore(preferences).load()
            assertTrue(ids.all { id -> cold.results.any { it.id == id } })
            openScreen { vm, _ ->
                assertTrue(ids.all { vm.uiState.value.pingResults[it] is PingResult.Error })
                assertTrue(ids.all { !(vm.uiState.value.pingResults[it] as PingResult.Error).currentNetwork })
            }
        } finally {
            server.close(); worker.join(2_000)
            ids.forEach { repo.deleteProfile(it) }
            if (selectedBefore != null) repo.setActiveProfile(selectedBefore) else repo.clearActiveProfile()
            preferences.setProxyOnlyMode(originalProxy)
        }
    }
}
