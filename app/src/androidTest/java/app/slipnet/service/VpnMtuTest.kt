package app.slipnet.service

import android.app.Activity
import android.content.Intent
import android.net.ConnectivityManager
import android.net.VpnService
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.os.ResultReceiver
import android.os.Build
import android.os.ParcelFileDescriptor
import androidx.core.content.ContextCompat
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.di.DatabaseModule
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import app.slipnet.domain.model.VpnMtu
import com.google.gson.Gson
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import kotlinx.coroutines.sync.withLock
import org.junit.Assert.*
import org.junit.Assume.assumeTrue
import org.junit.Test

class VpnMtuTest {
    @Test fun ipv4PreservesLowValuesAndIpv6HonoursItsMinimum() {
        assertEquals(512, VpnMtu.effective(512, blockIpv6 = true))
        assertEquals(512, VpnMtu.effective(1, blockIpv6 = true))
        assertEquals(1280, VpnMtu.effective(512, blockIpv6 = false))
        assertEquals(1280, VpnMtu.effective(1279, blockIpv6 = false))
        assertEquals(1400, VpnMtu.effective(1400, blockIpv6 = false))
        assertEquals(1500, VpnMtu.effective(9000, blockIpv6 = true))
    }

    /** Opt-in on a disposable device with VPN permission; checks the real interface and HEV startup. */
    @Test fun tunAndNativeForwarderAgreeAcrossIpv6Modes() = runBlocking<Unit> {
        assumeTrue(InstrumentationRegistry.getArguments().getString("testVpnMtu") == "true")
        val inst = InstrumentationRegistry.getInstrumentation()
        val context = inst.targetContext
        assertNull("Grant VPN consent on the test device first", VpnService.prepare(context))
        fun shell(command: String) = ParcelFileDescriptor.AutoCloseInputStream(
            inst.uiAutomation.executeShellCommand(command)
        ).bufferedReader().use { it.readText() }
        val networks = context.getSystemService(ConnectivityManager::class.java)
        fun tunMtu(): Int? {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                val tun = networks.allNetworks.mapNotNull(networks::getLinkProperties)
                    .firstOrNull { it.interfaceName?.matches(Regex("tun[0-9]+")) == true }
                if (tun != null) return tun.mtu
            }
            return shell("dumpsys connectivity").lineSequence().filter { it.contains("NetworkAgentInfo") }
                .mapNotNull { Regex("""InterfaceName: tun[0-9]+[^}]*?MTU: (\d+)""").find(it) }
                .firstOrNull()?.groupValues?.get(1)?.toInt()
        }
        fun nativeMtu(): Int? = Regex("""MTU: (\d+)""").findAll(shell(
            "logcat -d --pid=${android.os.Process.myPid()} -s HevSocks5Tunnel:I"
        )).lastOrNull()?.groupValues?.get(1)?.toInt()
        suspend fun until(condition: suspend () -> Boolean) = withTimeout(45_000) {
            while (!condition()) delay(250)
        }

        assertNull("A different VPN is already active", tunMtu())
        val prefs = PreferencesDataStore(context)
        val oldMtu = prefs.vpnMtu.first()
        val oldBlock = prefs.blockIpv6.first()
        val oldProxyOnly = prefs.proxyOnlyMode.first()
        val db = DatabaseModule.provideDatabase(context)
        val fixture = MtuSocksFixture()
        val profile = ServerProfile(name = "MTU test", tunnelType = TunnelType.SOCKS5,
            domain = "127.0.0.1", socks5ServerPort = fixture.port)
        val id = db.profileDao().insertProfile(ProfileMapper(Gson()).toEntity(profile))
        fun disconnect() = context.startService(Intent(context, SlipNetVpnService::class.java)
            .setAction(SlipNetVpnService.ACTION_DISCONNECT))
        try {
            prefs.setProxyOnlyMode(false)
            prefs.setVpnMtu(512)
            for ((block, expected) in listOf(false to 1280, true to 512)) {
                prefs.setBlockIpv6(block)
                assertEquals(512, prefs.vpnMtu.first())
                ContextCompat.startForegroundService(context, Intent(context, SlipNetVpnService::class.java)
                    .setAction(SlipNetVpnService.ACTION_CONNECT).putExtra(SlipNetVpnService.EXTRA_PROFILE_ID, id))
                until { tunMtu() == expected && nativeMtu() == expected }
                val received = CompletableDeferred<Pair<Int, Bundle>>()
                val callback = object : ResultReceiver(Handler(Looper.getMainLooper())) {
                    override fun onReceiveResult(code: Int, data: Bundle) { received.complete(code to data) }
                }
                context.startActivity(Intent().setClassName(inst.context.packageName, MtuTrafficActivity::class.java.name)
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK).putExtra("result", callback))
                val (code, response) = withTimeout(30_000) { received.await() }
                assertEquals(response.getString("error"), Activity.RESULT_OK, code)
                assertNotEquals(android.os.Process.myUid(), response.getInt("uid"))
                assertEquals("TCP reply through MTU $expected", fixture.payload, response.getString("body"))
                prefs.setVpnMtu(1400)
                assertEquals(expected, tunMtu())
                assertEquals(expected, nativeMtu())
                // Restore the requested value while the same interface is alive, before switching IPv6 mode.
                prefs.setVpnMtu(512)
                disconnect()
                // Android can keep a stopped VpnService bound. Its object lifetime is not the native cleanup barrier.
                until { tunMtu() == null && !app.slipnet.tunnel.HevSocks5Tunnel.isRunning() &&
                    TunnelOperations.mutex.withLock { TunnelOperations.owner == 0L } }
            }
        } finally {
            if (tunMtu() != null) disconnect()
            prefs.setVpnMtu(oldMtu)
            prefs.setBlockIpv6(oldBlock)
            prefs.setProxyOnlyMode(oldProxyOnly)
            db.profileDao().deleteProfile(id)
            db.close()
            fixture.close()
        }
    }
}
