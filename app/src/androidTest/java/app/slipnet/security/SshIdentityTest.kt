package app.slipnet.security

import android.content.Intent
import android.net.VpnService
import androidx.test.core.app.ActivityScenario
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.di.DatabaseModule
import app.slipnet.domain.model.ConnectionState
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import app.slipnet.presentation.MainActivity
import app.slipnet.service.VpnConnectionManager
import app.slipnet.tunnel.TransportTrust
import com.google.gson.Gson
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import org.junit.Assert.*
import org.junit.Test

class SshIdentityTest {
    /** Real SSH authentication and service lifecycle, through adb loopback peers. HTTPS is tested separately. */
    @Test fun requestedTestsReviewIdentitiesAndSubsequentConnectionsReusePins() = runBlocking<Unit> {
        val args = InstrumentationRegistry.getArguments()
        val plainPort = requireNotNull(args.getString("sshFixturePort")) { "Run the device gate with its SSH fixture" }.toInt()
        val tlsPort = requireNotNull(args.getString("tlsFixturePort")).toInt()
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        assertNull("Grant VPN consent on the disposable device first", VpnService.prepare(context))
        val preferences = PreferencesDataStore(context)
        val originalProxy = preferences.proxyOnlyMode.first()
        val db = DatabaseModule.provideDatabase(context)
        val mapper = ProfileMapper(Gson())
        val base = ServerProfile(name = "Synthetic SSH identity test", domain = "127.0.0.1", tunnelType = TunnelType.SSH,
            sshUsername = "device-test", sshPassword = "synthetic-only", sshPort = plainPort)
        val profiles = listOf(base, base.copy(name = "Synthetic STunTLS identity test", sshPort = tlsPort,
            sshTlsEnabled = true, sshTlsSni = "localhost")).map {
            it.copy(id = db.profileDao().insertProfile(mapper.toEntity(it)))
        }
        lateinit var manager: VpnConnectionManager
        var managerReady = false
        suspend fun connect(profile: ServerProfile, prompt: Boolean): Long = withContext(Dispatchers.Main) {
            manager.connect(profile, testMode = true, allowTrustPrompt = prompt)
        }
        suspend fun stop(id: Long) {
            withContext(Dispatchers.Main) { manager.disconnect(expectedSessionId = id) }
            withTimeout(30_000) { manager.awaitSessionDisconnected(id) }
            withContext(Dispatchers.Main) { manager.finishProbe(id) }
        }
        TransportTrust.clear()
        try {
            preferences.setProxyOnlyMode(false)
            ActivityScenario.launch<MainActivity>(Intent(context, MainActivity::class.java)).use { screen ->
                screen.onActivity { manager = it.connectionManager; managerReady = true }
                // Automatic tests still refuse the unknown peer without presenting or accepting a decision.
                val automatic = connect(profiles[0], prompt = false)
                assertTrue(automatic > 0)
                val refused = withTimeout(35_000) { manager.connectionState.first { it is ConnectionState.Error } }
                assertTrue(refused is ConnectionState.Error)
                assertEquals(app.slipnet.core.ProbeFailure.APPROVAL_REQUIRED,
                    app.slipnet.core.ProbeFailure.connection((refused as ConnectionState.Error).message))
                assertNull(TransportTrust.request.value)
                stop(automatic)
                for ((index, profile) in profiles.withIndex()) {
                    val session = connect(profile, prompt = true)
                    assertTrue(session > 0)
                    var decisions = 0
                    withTimeout(45_000) {
                        while (manager.connectionState.value !is ConnectionState.Connected) {
                            val state = manager.connectionState.value
                            assertFalse("SSH setup failed: $state", state is ConnectionState.Error)
                            TransportTrust.request.value?.let { request ->
                                // Exercise time waiting for a human rather than approving in the callback itself.
                                delay(250)
                                TransportTrust.answer(request, true)
                                decisions++
                                withTimeout(5_000) { TransportTrust.request.first { it !== request } }
                            }
                            delay(20)
                        }
                    }
                    assertEquals(if (index == 0) 1 else 2, decisions)
                    assertEquals(profile.id, (manager.connectionState.value as ConnectionState.Connected).profile.id)
                    stop(session)
                    val repeat = connect(profile, prompt = true)
                    withTimeout(35_000) {
                        while (manager.connectionState.value !is ConnectionState.Connected) {
                            assertNull("An approved unchanged identity prompted again", TransportTrust.request.value)
                            assertFalse(manager.connectionState.value is ConnectionState.Error)
                            delay(20)
                        }
                    }
                    stop(repeat)
                }
            }
        } finally {
            if (managerReady && !manager.connectionState.value.isDisconnected) runCatching { stop(manager.currentSessionId) }
            profiles.forEach { db.profileDao().deleteProfile(it.id) }
            db.close()
            preferences.setProxyOnlyMode(originalProxy)
            TransportTrust.allowPrompt = false
            TransportTrust.clear()
        }
    }
}
