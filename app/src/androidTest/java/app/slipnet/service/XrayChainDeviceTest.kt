package app.slipnet.service

import android.app.Activity
import android.content.Intent
import android.net.VpnService
import android.os.*
import androidx.test.core.app.ActivityScenario
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.core.CoreProcess
import app.slipnet.data.local.database.ChainEntity
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.di.DatabaseModule
import app.slipnet.domain.model.*
import app.slipnet.presentation.MainActivity
import app.slipnet.tunnel.*
import com.google.gson.Gson
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.first
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.net.ServerSocket

/** Actual service chain and cross-UID TUN traffic; only disposable loopback peers. */
class XrayChainDeviceTest {
    @Test fun xrayOuterCarriesSshAndTunUsesTheInnerEndpoint() = runBlocking<Unit> {
        val inst = InstrumentationRegistry.getInstrumentation()
        val context = inst.targetContext
        val sshPort = requireNotNull(InstrumentationRegistry.getArguments().getString("sshFixturePort")).toInt()
        assertNull(VpnService.prepare(context))
        val prefs = PreferencesDataStore(context)
        val oldProxy = prefs.proxyOnlyMode.first()
        val oldSplit = prefs.splitTunnelingEnabled.first()
        val oldBlock = prefs.blockIpv6.first()
        val db = DatabaseModule.provideDatabase(context)
        val server = CoreProcess(diagnostics = false)
        fun port() = ServerSocket(0).use { it.localPort }
        val vlessPort = port()
        val id = "00000000-0000-4000-8000-000000000018"
        val config = """{"inbounds":[{"listen":"127.0.0.1","port":${port()},"protocol":"socks","settings":{"auth":"noauth"}},{"listen":"127.0.0.1","port":$vlessPort,"protocol":"vless","settings":{"clients":[{"id":"$id"}],"decryption":"none"},"streamSettings":{"network":"raw","security":"none"}}],"outbounds":[{"protocol":"freedom"}]}"""
        val mapper = ProfileMapper(Gson())
        val profiles = listOf(
            ServerProfile(name = "Synthetic outer Xray", tunnelType = TunnelType.XRAY,
                coreLink = "vless://$id@127.0.0.1:$vlessPort?security=none&type=tcp#Synthetic"),
            ServerProfile(name = "Synthetic inner SSH", tunnelType = TunnelType.SSH, domain = "127.0.0.1",
                sshPort = sshPort, sshUsername = "device-test", sshPassword = "synthetic-only")
        ).map { it.copy(id = db.profileDao().insertProfile(mapper.toEntity(it))) }
        val now = System.currentTimeMillis()
        val chainId = db.chainDao().insertChecked(ChainEntity(name = "Synthetic Xray SSH",
            profileIdsJson = org.json.JSONArray(profiles.map { it.id }).toString(), createdAt = now, updatedAt = now))
        var manager: VpnConnectionManager? = null
        suspend fun stop() {
            manager?.let { connection ->
                withContext(Dispatchers.Main) { connection.disconnect() }
                withTimeout(30000) { connection.awaitSessionDisconnected(connection.currentSessionId) }
            }
        }
        TransportTrust.clear()
        try {
            server.start(File(context.applicationInfo.nativeLibraryDir, "libveyra_xray.so"), config,
                File(context.applicationInfo.nativeLibraryDir, "libveyra_core_launcher.so")).getOrThrow()
            prefs.setProxyOnlyMode(false); prefs.setSplitTunnelingEnabled(false); prefs.setBlockIpv6(true)
            TransportTrust.allowPrompt = true
            ActivityScenario.launch(MainActivity::class.java).use { screen ->
                screen.onActivity { manager = it.connectionManager }
                val connection = requireNotNull(manager)
                withContext(Dispatchers.Main) {
                    connection.connectChain(ProfileChain(id = chainId, name = "Synthetic Xray SSH", profileIds = profiles.map { it.id }), profiles.first())
                }
                withTimeout(45000) {
                    while ((connection.connectionState.value as? ConnectionState.Connected)?.chainId != chainId) {
                        assertFalse("Chain setup failed: ${connection.connectionState.value}", connection.connectionState.value is ConnectionState.Error)
                        TransportTrust.request.value?.let { TransportTrust.answer(it, true) }
                        delay(25)
                    }
                }
                assertEquals(chainId, (connection.connectionState.value as ConnectionState.Connected).chainId)
                val response = CompletableDeferred<Pair<Int, Bundle>>()
                val receiver = object : ResultReceiver(Handler(Looper.getMainLooper())) {
                    override fun onReceiveResult(code: Int, data: Bundle) { response.complete(code to data) }
                }
                context.startActivity(Intent().setClassName(inst.context.packageName, MtuTrafficActivity::class.java.name)
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK).putExtra("result", receiver))
                val (code, body) = withTimeout(15000) { response.await() }
                assertEquals(body.getString("error"), Activity.RESULT_OK, code)
                assertNotEquals(android.os.Process.myUid(), body.getInt("uid"))
                assertTrue(body.getBoolean("vpn"))
                // Only the SSH peer can produce this response. Bypassing the inner
                // endpoint and feeding HEV from outer Xray cannot pass this check.
                assertEquals("chain-through-inner-ssh\n", body.getString("body"))
                stop()
                assertFalse(HevSocks5Tunnel.isRunning())
                assertFalse(XrayBridge.isRunning())
                assertTrue("Stopping the client chain killed an independent core", server.isRunning())
            }
        } finally {
            runCatching { stop() }
            server.stop()
            db.chainDao().deleteChain(chainId)
            profiles.forEach { db.profileDao().deleteProfile(it.id) }
            db.close()
            prefs.setProxyOnlyMode(oldProxy); prefs.setSplitTunnelingEnabled(oldSplit); prefs.setBlockIpv6(oldBlock)
            TransportTrust.allowPrompt = false; TransportTrust.clear()
        }
    }
}
