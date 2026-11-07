package app.slipnet.service

import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.R
import app.slipnet.domain.model.ConnectionIndicator
import app.slipnet.domain.model.ConnectionState
import app.slipnet.domain.model.ServerProfile
import org.junit.Assert.*
import org.junit.Test

class NotificationStatusDeviceTest {
    @Test fun statusShapesAndAccentFollowActualConnectionState() {
        val helper = NotificationHelper(InstrumentationRegistry.getInstrumentation().targetContext)
        val profile = ServerProfile(name = "synthetic")
        val cases = listOf(
            Triple(ConnectionState.Connected(profile), R.drawable.ic_rovunet_status_connected, 0xFF4CAF50.toInt()),
            Triple(ConnectionState.Error("synthetic"), R.drawable.ic_rovunet_status_error, 0xFFF44336.toInt()),
            Triple(ConnectionState.Disconnected, R.drawable.ic_rovunet_status, 0xFF9E9E9E.toInt()),
            Triple(ConnectionState.Connecting, R.drawable.ic_rovunet_status_connecting, 0xFFFF9800.toInt()),
            Triple(ConnectionState.Disconnecting, R.drawable.ic_rovunet_status_disconnecting, 0xFFFF9800.toInt()))
        for ((state, icon, color) in cases) {
            val notification = helper.createVpnNotification(state)
            assertEquals(icon, notification.smallIcon.resId)
            assertEquals(color, notification.color)
            assertFalse(notification.extras.getBoolean("android.colorized", false))
        }
        assertEquals(R.drawable.ic_rovunet_status_connecting,
            helper.createAutoReconnectNotification("synthetic", 1, 3).smallIcon.resId)
        assertEquals(R.drawable.ic_rovunet_status_error,
            helper.createKillSwitchNotification("synthetic").smallIcon.resId)
        // A failed background probe is not a terminal connection error.
        assertEquals(R.drawable.ic_rovunet_status,
            helper.createProbeFailNotification(1L).smallIcon.resId)
        assertEquals(ConnectionIndicator.DISCONNECTED, ConnectionState.Disconnected.indicator)
    }
}
