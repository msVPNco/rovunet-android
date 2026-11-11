package app.slipnet.service

import android.content.Intent
import android.os.SystemClock
import androidx.lifecycle.ViewModelProvider
import androidx.test.core.app.ActivityScenario
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.domain.model.ConnectionState
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import app.slipnet.presentation.MainActivity
import app.slipnet.presentation.main.MainViewModel
import java.net.ServerSocket
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.concurrent.thread
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.first
import org.junit.Assert.*
import org.junit.Test

class ServiceStartupCancellationDeviceTest {
    /** Goes through manager -> Android service -> pending JSch I/O -> teardown, not instance.stop(). */
    @Test fun disconnectIntentAbortsPendingSshBeforeJoiningStartup() = runBlocking<Unit> {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val context = instrumentation.targetContext
        val preferences = PreferencesDataStore(context)
        val originalProxy = preferences.proxyOnlyMode.first()
        ServerSocket(0).use { upstream ->
            val accepted = CountDownLatch(1)
            val closed = CountDownLatch(1)
            val peer = thread(isDaemon = true, name = "service-silent-ssh") {
                runCatching {
                    upstream.accept().use { socket ->
                        socket.soTimeout = 15_000
                        accepted.countDown()
                        // Consume the client banner but never return a server banner.
                        while (socket.getInputStream().read() != -1) { }
                        closed.countDown()
                    }
                }
            }
            preferences.setProxyOnlyMode(true)
            try {
                ActivityScenario.launch<MainActivity>(Intent(context, MainActivity::class.java)).use { scenario ->
                    lateinit var vm: MainViewModel
                    lateinit var manager: VpnConnectionManager
                    scenario.onActivity { vm = ViewModelProvider(it)[MainViewModel::class.java]; manager = it.connectionManager }
                    withTimeout(30_000) { vm.uiState.first { !it.isLoading } }
                    val id = vm.profileRepository.insertCollection(listOf(ServerProfile(name = "Synthetic silent SSH",
                        domain = "127.0.0.1", sshPort = upstream.localPort, tunnelType = TunnelType.SSH,
                        sshUsername = "device-test", sshPassword = "synthetic-only"))).single()
                    try {
                        val profile = requireNotNull(vm.profileRepository.getProfileById(id))
                        withContext(Dispatchers.Main) { vm.connect(profile) }
                        assertTrue("Service never opened its SSH socket", withContext(Dispatchers.IO) { accepted.await(15, TimeUnit.SECONDS) })
                        val session = manager.currentSessionId
                        val before = SystemClock.elapsedRealtime()
                        withContext(Dispatchers.Main) { vm.disconnect() }
                        withTimeout(6000) { manager.awaitSessionDisconnected(session) }
                        assertTrue("Stop waited for the 30-second SSH timeout", SystemClock.elapsedRealtime() - before < 5000)
                        assertTrue("Pending socket was not closed", withContext(Dispatchers.IO) { closed.await(2, TimeUnit.SECONDS) })
                        assertTrue(manager.connectionState.value is ConnectionState.Disconnected)
                    } finally {
                        withContext(Dispatchers.Main) { manager.disconnect() }
                        withTimeout(15_000) { manager.awaitSessionDisconnected(manager.currentSessionId) }
                        vm.profileRepository.deleteProfile(id)
                    }
                }
            } finally { preferences.setProxyOnlyMode(originalProxy); upstream.close(); peer.join(1000) }
        }
    }
}
