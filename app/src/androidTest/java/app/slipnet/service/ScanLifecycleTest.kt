package app.slipnet.service

import android.app.NotificationManager
import android.content.Intent
import android.content.pm.ActivityInfo
import android.content.res.Configuration
import android.os.ParcelFileDescriptor
import androidx.core.content.ContextCompat
import androidx.lifecycle.ViewModelProvider
import androidx.test.core.app.ActivityScenario
import app.slipnet.presentation.MainActivity
import app.slipnet.presentation.scanner.DnsScannerViewModel
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.awaitCancellation
import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import org.junit.Assert.*
import org.junit.Test

class ScanLifecycleTest {
    private val inst = InstrumentationRegistry.getInstrumentation()
    private val context = inst.targetContext
    private val notifications = context.getSystemService(NotificationManager::class.java)
    private fun notification() = notifications.activeNotifications.firstOrNull {
        it.id == NotificationHelper.SCAN_NOTIFICATION_ID
    }?.notification

    private fun wakeLockHeld(): Boolean {
        val fd = inst.uiAutomation.executeShellCommand("dumpsys power")
        return ParcelFileDescriptor.AutoCloseInputStream(fd).bufferedReader().useLines { lines ->
            lines.any { "PARTIAL_WAKE_LOCK" in it && "'Rovunet:ScanWakeLock'" in it }
        }
    }

    private fun startService() {
        ContextCompat.startForegroundService(context, Intent(context, ScanService::class.java))
    }

    private suspend fun until(condition: () -> Boolean) = withTimeout(25_000) {
        while (!condition()) delay(100)
    }

    @Test fun notificationStopsBothWorkersWithoutViewModelAndReleasesWakeLock() = runBlocking<Unit> {
        val run = ScanStateHolder.begin()
        try {
            run.update { it.copy(isScanning = true, isE2eRunning = true) }
            val dns = run.launch(ScanPhase.DNS) { awaitCancellation() }
            val e2e = run.launch(ScanPhase.E2E) { awaitCancellation() }
            startService()
            until { notification() != null && wakeLockHeld() }
            requireNotNull(notification()).actions.single().actionIntent.send()
            until { dns.isCompleted && e2e.isCompleted && !run.state.value.active }
            until { notification() == null && !wakeLockHeld() }
        } finally { run.stop() }
    }

    @Test fun recreatedScreenKeepsScanAndExplicitExitReleasesIt() = runBlocking<Unit> {
        val run = ScanStateHolder.begin()
        try {
            run.update { it.copy(isScanning = true) }
            val worker = run.launch(ScanPhase.DNS) { awaitCancellation() }
            startService()
            until { notification() != null && wakeLockHeld() }
            ActivityScenario.launch(MainActivity::class.java).use { screen ->
                var target = Configuration.ORIENTATION_LANDSCAPE
                screen.onActivity {
                    ViewModelProvider(it)[DnsScannerViewModel::class.java]
                    val portrait = it.resources.configuration.orientation == Configuration.ORIENTATION_PORTRAIT
                    target = if (portrait) Configuration.ORIENTATION_LANDSCAPE else Configuration.ORIENTATION_PORTRAIT
                    it.requestedOrientation = if (portrait) ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE else ActivityInfo.SCREEN_ORIENTATION_PORTRAIT
                }
                until {
                    var rotated = false
                    screen.onActivity { rotated = it.resources.configuration.orientation == target }
                    rotated
                }
                assertTrue(run.state.value.isScanning && wakeLockHeld())
                screen.onActivity { ViewModelProvider(it)[DnsScannerViewModel::class.java].stopAll() }
            }
            until { worker.isCompleted && !run.state.value.active && notification() == null && !wakeLockHeld() }
        } finally { run.stop() }
    }

    @Test fun removingTheTaskKeepsTheRunButStoppingTheServiceReleasesIt() = runBlocking<Unit> {
        val run = ScanStateHolder.begin()
        try {
            run.update { it.copy(isScanning = true) }
            val worker = run.launch(ScanPhase.DNS) { awaitCancellation() }
            startService()
            until { notification() != null && wakeLockHeld() }
            ActivityScenario.launch(MainActivity::class.java).use { screen ->
                lateinit var activity: MainActivity
                screen.onActivity { activity = it; ViewModelProvider(it)[DnsScannerViewModel::class.java] }
                inst.runOnMainSync { activity.moveTaskToBack(true) }
                assertTrue(run.state.value.active && wakeLockHeld())
                inst.runOnMainSync { activity.finishAndRemoveTask() }
                until { activity.isDestroyed }
                assertTrue(worker.isActive && notification() != null && wakeLockHeld())
            }
            assertTrue(context.stopService(Intent(context, ScanService::class.java)))
            until { worker.isCompleted && !run.state.value.active && notification() == null && !wakeLockHeld() }
        } finally { run.stop() }
    }

    @Test fun oldStopCannotCancelReplacementAndNaturalCompletionReleasesResources() = runBlocking<Unit> {
        val first = ScanStateHolder.begin()
        try {
            first.update { it.copy(isScanning = true) }
            startService()
            until { notification() != null && wakeLockHeld() }
            val oldStop = requireNotNull(notification()).actions.single().actionIntent
            val next = ScanStateHolder.begin()
            next.update { it.copy(isScanning = true, isE2eRunning = true) }
            next.launch(ScanPhase.DNS) { awaitCancellation() }
            val complete = CompletableDeferred<Unit>()
            next.launch(ScanPhase.E2E) { complete.await() }
            startService()
            until { notification()?.actions?.single()?.actionIntent?.let { it != oldStop } == true }
            oldStop.send()
            next.stopPhase(ScanPhase.DNS)
            until { !next.state.value.isScanning && next.state.value.isE2eRunning && wakeLockHeld() }
            complete.complete(Unit)
            until { !next.state.value.active && notification() == null && !wakeLockHeld() }
        } finally { ScanStateHolder.current.stop() }
    }

    @Test fun rapidRestartWaitsForOldNativeCleanupAndRejectsStaleProgress() = runBlocking<Unit> {
        val first = ScanStateHolder.begin()
        val cleaning = CompletableDeferred<Unit>()
        val enter = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        try {
            first.update { it.copy(isScanning = true) }
            first.launch(ScanPhase.DNS) {
                try { enter.complete(Unit); awaitCancellation() } finally {
                    withContext(NonCancellable) { cleaning.complete(Unit); release.await() }
                }
            }
            withTimeout(10_000) { enter.await() }
            first.stop(clearResults = true)
            withTimeout(10_000) { cleaning.await() }
            ScanStateHolder.begin()
            val next = ScanStateHolder.begin()
            next.update { it.copy(isScanning = true, totalCount = 5) }
            val started = CompletableDeferred<Unit>()
            next.launch(ScanPhase.DNS) { started.complete(Unit) }
            first.update { it.copy(isScanning = true, totalCount = 99) }
            assertFalse(first.state.value.active)
            assertEquals(5, next.state.value.totalCount)
            assertFalse(started.isCompleted)
            release.complete(Unit)
            withTimeout(10_000) { started.await() }
            until { !next.state.value.active }
        } finally { release.complete(Unit); ScanStateHolder.current.stop() }
    }
}
