package app.slipnet.service

import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import javax.inject.Inject

/** Owns the notification and CPU wake lock for the lifetime of the active DNS/E2E run. */
@AndroidEntryPoint
class ScanService : Service() {
    companion object {
        const val ACTION_STOP = "app.slipnet.SCAN_STOP"
        private const val WAKE_TIMEOUT_MS = 4 * 60 * 60 * 1000L
    }

    @Inject lateinit var notificationHelper: NotificationHelper
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private var observer: Job? = null
    private var owner: ScanRun? = null
    private val wakeLock by lazy {
        getSystemService(PowerManager::class.java).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "Rovunet:ScanWakeLock")
            .apply { setReferenceCounted(false) }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val run = ScanStateHolder.current
        if (intent?.action == ACTION_STOP && intent.data?.schemeSpecificPart == run.id) run.stop()
        observer?.cancel()
        val initial = run.state.value
        // Even a run cancelled before start delivery must satisfy startForegroundService's contract.
        val notification = notification(run, initial)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NotificationHelper.SCAN_NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC)
        } else {
            startForeground(NotificationHelper.SCAN_NOTIFICATION_ID, notification)
        }
        if (owner !== run && wakeLock.isHeld) wakeLock.release()
        owner = run
        if (!initial.active) {
            finish(startId)
            return START_NOT_STICKY
        }

        if (!wakeLock.isHeld) wakeLock.acquire(WAKE_TIMEOUT_MS)
        observer = scope.launch {
            run.state.collect { state ->
                if (owner !== run) return@collect
                if (state.active) {
                    getSystemService(NotificationManager::class.java)
                        .notify(NotificationHelper.SCAN_NOTIFICATION_ID, notification(run, state))
                } else {
                    finish(startId)
                }
            }
        }
        return START_NOT_STICKY
    }

    private fun notification(run: ScanRun, state: ScanServiceState) =
        notificationHelper.createScanNotification(
            run.id, state.scannedCount, state.totalCount, state.workingCount,
            state.isE2eRunning, state.isScanning, state.e2eTestedCount, state.e2eTotalCount, state.e2ePassedCount
        )

    private fun finish(startId: Int) {
        owner = null
        if (wakeLock.isHeld) wakeLock.release()
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelfResult(startId)
    }

    override fun onTimeout(startId: Int, fgsType: Int) {
        owner?.stop()
        finish(startId)
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        scope.cancel()
        owner?.stop()
        if (wakeLock.isHeld) wakeLock.release()
        super.onDestroy()
    }
}
