package app.slipnet.service

import android.app.Notification
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import app.slipnet.R
import app.slipnet.SlipNetApp
import app.slipnet.domain.model.ConnectionState
import app.slipnet.domain.model.TrafficStats
import app.slipnet.presentation.MainActivity
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class NotificationHelper @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private val localized get() = app.slipnet.presentation.theme.RovunetStrings.localizedContext(context)

    companion object {
        const val VPN_NOTIFICATION_ID = 1
        const val RECONNECT_NOTIFICATION_ID = 2
        const val DISCONNECT_NOTIFICATION_ID = 3
        private const val REQUEST_CODE_MAIN = 100
        private const val REQUEST_CODE_DISCONNECT = 101
        private const val REQUEST_CODE_RECONNECT = 102
        private const val REQUEST_CODE_RECONNECT_DISCONNECT = 103
        private const val REQUEST_CODE_AUTO_RECONNECT_CANCEL = 104
        const val AUTO_RECONNECT_NOTIFICATION_ID = 4
        const val PROBE_FAIL_NOTIFICATION_ID = 5
        private const val REQUEST_CODE_PROBE_RECONNECT = 105
        const val SCAN_NOTIFICATION_ID = 6
        private const val REQUEST_CODE_SCAN_STOP = 106
        private const val REQUEST_CODE_BOOT_RETRY_CANCEL = 107
        private const val REQUEST_CODE_SCAN = 108
    }

    private fun createServicePendingIntent(
        requestCode: Int,
        intent: Intent,
        foreground: Boolean = false
    ): PendingIntent {
        val flags = PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        return if (foreground && Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            PendingIntent.getForegroundService(context, requestCode, intent, flags)
        } else {
            PendingIntent.getService(context, requestCode, intent, flags)
        }
    }

    fun createVpnNotification(
        state: ConnectionState,
        isProxyOnly: Boolean = false,
        trafficStats: TrafficStats? = null,
        uploadSpeed: Long = 0,
        downloadSpeed: Long = 0
    ): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_MAIN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val builder = NotificationCompat.Builder(context, SlipNetApp.CHANNEL_VPN_STATUS)
            .setSmallIcon(R.drawable.ic_rovunet_status)
            .setBadgeIconType(if (state is ConnectionState.Connected) NotificationCompat.BADGE_ICON_SMALL else NotificationCompat.BADGE_ICON_NONE)
            .setNumber(if (state is ConnectionState.Connected) 1 else 0)
            .setContentIntent(mainPendingIntent)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setSortKey("0") // Pin position so frequent updates don't cause reordering
            .setWhen(0) // Fixed timestamp prevents MIUI/HyperOS from reordering on update
            .setShowWhen(false)

        when (state) {
            is ConnectionState.Disconnected -> {
                builder
                    .setContentTitle("Rovunet")
                    .setContentText(app.slipnet.presentation.theme.RovunetStrings.text(context, "Disconnected"))
            }
            is ConnectionState.Connecting -> {
                builder
                    .setContentTitle("Rovunet")
                    .setContentText(app.slipnet.presentation.theme.RovunetStrings.text(context, "Connecting..."))
                    .setProgress(0, 0, true)
            }
            is ConnectionState.Connected -> {
                val disconnectIntent = Intent(context, SlipNetVpnService::class.java).apply {
                    action = SlipNetVpnService.ACTION_DISCONNECT
                }
                val disconnectPendingIntent = createServicePendingIntent(
                    requestCode = REQUEST_CODE_DISCONNECT,
                    intent = disconnectIntent
                )

                val reconnectIntent = Intent(context, SlipNetVpnService::class.java).apply {
                    action = SlipNetVpnService.ACTION_RECONNECT
                }
                val reconnectPendingIntent = createServicePendingIntent(
                    requestCode = REQUEST_CODE_RECONNECT,
                    intent = reconnectIntent
                )

                val hasTraffic = trafficStats != null && trafficStats.totalBytes > 0

                val bodyText = if (hasTraffic) {
                    val upLine = "\u2191 ${trafficStats!!.formatBytesSent()}  ${TrafficStats.formatSpeed(uploadSpeed)}"
                    val downLine = "\u2193 ${trafficStats.formatBytesReceived()}  ${TrafficStats.formatSpeed(downloadSpeed)}"
                    "$upLine\n$downLine"
                } else {
                    if (isProxyOnly) app.slipnet.presentation.theme.RovunetStrings.text(context, "Proxy is active") else app.slipnet.presentation.theme.RovunetStrings.text(context, "VPN is active")
                }

                if (hasTraffic) {
                    builder.setStyle(NotificationCompat.BigTextStyle().bigText(bodyText))
                }

                builder
                    .setContentTitle(localized.getString(R.string.notification_connected, state.profile.name))
                    .setContentText(bodyText)
                    .addAction(
                        R.drawable.ic_rovunet_status,
                        localized.getString(R.string.rv_reconnect),
                        reconnectPendingIntent
                    )
                    .addAction(
                        R.drawable.ic_rovunet_status,
                        app.slipnet.presentation.theme.RovunetStrings.text(context, "Disconnect"),
                        disconnectPendingIntent
                    )
            }
            is ConnectionState.Disconnecting -> {
                builder
                    .setContentTitle("Rovunet")
                    .setContentText(app.slipnet.presentation.theme.RovunetStrings.text(context, "Disconnecting..."))
                    .setProgress(0, 0, true)
            }
            is ConnectionState.Error -> {
                builder
                    .setContentTitle("Rovunet")
                    .setContentText(localized.getString(R.string.notification_error, state.message))
            }
        }

        return builder.build()
    }

    fun createKillSwitchNotification(profileName: String): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_MAIN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val disconnectIntent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_DISCONNECT
        }
        val disconnectPendingIntent = createServicePendingIntent(
            requestCode = REQUEST_CODE_DISCONNECT,
            intent = disconnectIntent
        )

        return NotificationCompat.Builder(context, SlipNetApp.CHANNEL_VPN_STATUS)
            .setSmallIcon(R.drawable.ic_rovunet_status)
            .setContentTitle(localized.getString(R.string.rv_traffic_blocked_kill_switch_active_1b0a8a))
            .setContentText(localized.getString(R.string.notification_reconnecting, profileName))
            .setContentIntent(mainPendingIntent)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .addAction(R.drawable.ic_rovunet_status, app.slipnet.presentation.theme.RovunetStrings.text(context, "Disconnect"), disconnectPendingIntent)
            .build()
    }

    fun createAutoReconnectNotification(
        profileName: String,
        attempt: Int,
        maxAttempts: Int
    ): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_MAIN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val disconnectIntent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_DISCONNECT
        }
        val disconnectPendingIntent = createServicePendingIntent(
            requestCode = REQUEST_CODE_AUTO_RECONNECT_CANCEL,
            intent = disconnectIntent
        )

        return NotificationCompat.Builder(context, SlipNetApp.CHANNEL_VPN_STATUS)
            .setSmallIcon(R.drawable.ic_rovunet_status)
            .setContentTitle(localized.getString(R.string.notification_reconnecting, profileName))
            .setContentText(localized.getString(R.string.notification_attempt, attempt, maxAttempts))
            .setContentIntent(mainPendingIntent)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .addAction(R.drawable.ic_rovunet_status, app.slipnet.presentation.theme.RovunetStrings.text(context, "Cancel"), disconnectPendingIntent)
            .build()
    }

    fun createBootRetryNotification(
        profileName: String,
        attempt: Int,
        maxAttempts: Int
    ): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_MAIN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val disconnectIntent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_DISCONNECT
        }
        val disconnectPendingIntent = createServicePendingIntent(
            requestCode = REQUEST_CODE_BOOT_RETRY_CANCEL,
            intent = disconnectIntent
        )

        return NotificationCompat.Builder(context, SlipNetApp.CHANNEL_VPN_STATUS)
            .setSmallIcon(R.drawable.ic_rovunet_status)
            .setContentTitle(localized.getString(R.string.rv_waiting_for_network_d1c557))
            .setContentText(localized.getString(R.string.notification_attempt, attempt, maxAttempts) + " — " + profileName.ifEmpty { "VPN" })
            .setContentIntent(mainPendingIntent)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .addAction(R.drawable.ic_rovunet_status, app.slipnet.presentation.theme.RovunetStrings.text(context, "Cancel"), disconnectPendingIntent)
            .build()
    }

    fun createReconnectNotification(
        message: String,
        profileId: Long
    ): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_MAIN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val reconnectIntent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_CONNECT
            putExtra(SlipNetVpnService.EXTRA_PROFILE_ID, profileId)
        }
        val reconnectPendingIntent = createServicePendingIntent(
            requestCode = REQUEST_CODE_RECONNECT,
            intent = reconnectIntent,
            foreground = true
        )

        return NotificationCompat.Builder(context, SlipNetApp.CHANNEL_CONNECTION_EVENTS)
            .setSmallIcon(R.drawable.ic_rovunet_status)
            .setContentTitle(app.slipnet.presentation.theme.RovunetStrings.text(context, "Connection Lost"))
            .setContentText(app.slipnet.presentation.theme.RovunetStrings.text(context, message))
            .setContentIntent(mainPendingIntent)
            .addAction(R.drawable.ic_rovunet_status, localized.getString(R.string.rv_reconnect), reconnectPendingIntent)
            .setAutoCancel(true)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()
    }

    fun createSmartConnectNotification(
        transportName: String,
        attempt: Int,
        total: Int
    ): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_MAIN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        return NotificationCompat.Builder(context, SlipNetApp.CHANNEL_VPN_STATUS)
            .setSmallIcon(R.drawable.ic_rovunet_status)
            .setContentTitle(localized.getString(R.string.notification_smart, transportName))
            .setContentText(localized.getString(R.string.notification_attempt, attempt, total))
            .setContentIntent(mainPendingIntent)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setProgress(total, attempt, false)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()
    }

    fun createDisconnectedNotification(
        profileName: String,
        profileId: Long
    ): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_MAIN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val reconnectIntent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_CONNECT
            putExtra(SlipNetVpnService.EXTRA_PROFILE_ID, profileId)
        }
        val reconnectPendingIntent = createServicePendingIntent(
            requestCode = REQUEST_CODE_RECONNECT_DISCONNECT,
            intent = reconnectIntent,
            foreground = true
        )

        return NotificationCompat.Builder(context, SlipNetApp.CHANNEL_CONNECTION_EVENTS)
            .setSmallIcon(R.drawable.ic_rovunet_status)
            .setContentTitle(app.slipnet.presentation.theme.RovunetStrings.text(context, "VPN Disconnected"))
            .setContentText(localized.getString(R.string.notification_interrupted, profileName))
            .setContentIntent(mainPendingIntent)
            .addAction(R.drawable.ic_rovunet_status, localized.getString(R.string.rv_reconnect), reconnectPendingIntent)
            .setAutoCancel(true)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()
    }

    fun createProbeFailNotification(profileId: Long): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_MAIN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val reconnectIntent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_CONNECT
            putExtra(SlipNetVpnService.EXTRA_PROFILE_ID, profileId)
        }
        val reconnectPendingIntent = createServicePendingIntent(
            requestCode = REQUEST_CODE_PROBE_RECONNECT,
            intent = reconnectIntent,
            foreground = true
        )

        return NotificationCompat.Builder(context, SlipNetApp.CHANNEL_CONNECTION_EVENTS)
            .setSmallIcon(R.drawable.ic_rovunet_status)
            .setContentTitle(app.slipnet.presentation.theme.RovunetStrings.text(context, "Tunnel not passing traffic"))
            .setContentText(app.slipnet.presentation.theme.RovunetStrings.text(context, "The VPN tunnel appears to be broken"))
            .setContentIntent(mainPendingIntent)
            .addAction(R.drawable.ic_rovunet_status, localized.getString(R.string.rv_reconnect), reconnectPendingIntent)
            .setAutoCancel(true)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()
    }

    fun createScanNotification(
        scannedCount: Int,
        totalCount: Int,
        workingCount: Int,
        isE2eRunning: Boolean = false,
        isScanning: Boolean = true,
        e2eTestedCount: Int = 0,
        e2eTotalCount: Int = 0,
        e2ePassedCount: Int = 0
    ): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_SCAN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val stopIntent = Intent(context, ScanService::class.java).apply {
            action = ScanService.ACTION_STOP
        }
        val stopPendingIntent = createServicePendingIntent(
            requestCode = REQUEST_CODE_SCAN_STOP,
            intent = stopIntent
        )

        val title = when {
            isE2eRunning && !isScanning -> localized.getString(R.string.notification_testing)
            isE2eRunning -> localized.getString(R.string.notification_scanning_testing)
            else -> localized.getString(R.string.notification_scanning)
        }
        val text = when {
            isE2eRunning && !isScanning && e2eTotalCount > 0 ->
                localized.getString(R.string.notification_test_count, e2eTestedCount, e2eTotalCount, e2ePassedCount)
            totalCount > 0 ->
                localized.getString(R.string.notification_scan_count, scannedCount, totalCount, workingCount)
            else -> localized.getString(R.string.notification_scan_start)
        }

        return NotificationCompat.Builder(context, SlipNetApp.CHANNEL_SCAN_STATUS)
            .setSmallIcon(R.drawable.ic_search)
            .setContentTitle(app.slipnet.presentation.theme.RovunetStrings.text(context, title))
            .setContentText(text)
            .setContentIntent(mainPendingIntent)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setProgress(totalCount, scannedCount, totalCount == 0)
            .addAction(R.drawable.ic_search, app.slipnet.presentation.theme.RovunetStrings.text(context, "Stop"), stopPendingIntent)
            .build()
    }

    fun createConnectionEventNotification(
        title: String,
        message: String
    ): Notification {
        val mainIntent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            context,
            REQUEST_CODE_MAIN,
            mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        return NotificationCompat.Builder(context, SlipNetApp.CHANNEL_CONNECTION_EVENTS)
            .setSmallIcon(R.drawable.ic_rovunet_status)
            .setContentTitle(app.slipnet.presentation.theme.RovunetStrings.text(context, title))
            .setContentText(app.slipnet.presentation.theme.RovunetStrings.text(context, message))
            .setContentIntent(mainPendingIntent)
            .setAutoCancel(true)
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .build()
    }
}
