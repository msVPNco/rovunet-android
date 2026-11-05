package app.slipnet

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.os.Build
import app.slipnet.data.local.datastore.PreferencesDataStore
import dagger.hilt.android.HiltAndroidApp
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltAndroidApp
class SlipNetApp : Application() {

    @Inject lateinit var preferencesDataStore: PreferencesDataStore

    private val appScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    override fun onCreate() {
        super.onCreate()
        app.slipnet.util.StabilityDiagnostics.initialize(this)
        app.slipnet.tunnel.TransportTrust.initialize(this)
        createNotificationChannels()
        appScope.launch { preferencesDataStore.ensureProxyPortInitialized() }
    }

    private fun createNotificationChannels() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val notificationManager = getSystemService(NotificationManager::class.java)
            val resources = app.slipnet.presentation.theme.RovunetStrings.localizedContext(this).resources

            // VPN Status Channel
            val vpnChannel = NotificationChannel(
                CHANNEL_VPN_STATUS,
                resources.getString(R.string.settings_vpn_status_848bdf),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = resources.getString(R.string.settings_shows_current_vpn_connection_status_502795)
                setShowBadge(true)
            }
            notificationManager.createNotificationChannel(vpnChannel)

            // Connection Events Channel
            val eventsChannel = NotificationChannel(
                CHANNEL_CONNECTION_EVENTS,
                resources.getString(R.string.settings_connection_events_5313dd),
                NotificationManager.IMPORTANCE_DEFAULT
            ).apply {
                description = resources.getString(R.string.settings_notifications_for_vpn_connection_events_120e7d)
            }
            notificationManager.createNotificationChannel(eventsChannel)

            // DNS Scan Status Channel
            val scanChannel = NotificationChannel(
                CHANNEL_SCAN_STATUS,
                resources.getString(R.string.settings_dns_scan_33ff80),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = resources.getString(R.string.settings_shows_dns_resolver_scan_progress_744073)
                setShowBadge(false)
            }
            notificationManager.createNotificationChannel(scanChannel)
        }
    }

    companion object {
        const val CHANNEL_VPN_STATUS = "vpn_status"
        const val CHANNEL_CONNECTION_EVENTS = "connection_events"
        const val CHANNEL_SCAN_STATUS = "scan_status"
    }
}
