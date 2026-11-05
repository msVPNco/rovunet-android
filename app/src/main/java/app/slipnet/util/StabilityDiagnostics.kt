package app.slipnet.util

import android.app.Activity
import android.app.ActivityManager
import android.app.Application
import android.os.Build
import android.os.Bundle
import android.os.Debug
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import app.slipnet.BuildConfig
import app.slipnet.core.MainThreadStall
import app.slipnet.core.StabilityReport
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.collectLatest
import java.io.File
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicLong

/** Private, bounded reports. Never uploads anything or copies ordinary connection logs. */
object StabilityDiagnostics {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val resumed = AtomicInteger()
    private val foreground = MutableStateFlow(false)
    private val heartbeat = AtomicLong()
    private val foregroundSince = AtomicLong()
    private val pending = java.util.concurrent.atomic.AtomicBoolean()
    @Volatile private var initialized = false
    private lateinit var directory: File

    @Synchronized fun initialize(application: Application) {
        if (initialized) return
        initialized = true
        directory = File(application.filesDir, "stability")
        val main = Looper.getMainLooper()
        val handler = Handler(main)
        val beat = Runnable { pending.set(false) }
        heartbeat.set(SystemClock.uptimeMillis())
        application.registerActivityLifecycleCallbacks(object : Application.ActivityLifecycleCallbacks {
            override fun onActivityResumed(activity: Activity) { foregroundSince.set(SystemClock.uptimeMillis()); resumed.incrementAndGet(); foreground.value = true }
            override fun onActivityPaused(activity: Activity) { foreground.value = resumed.updateAndGet { (it - 1).coerceAtLeast(0) } > 0 }
            override fun onActivityCreated(activity: Activity, state: Bundle?) = Unit
            override fun onActivityStarted(activity: Activity) = Unit
            override fun onActivityStopped(activity: Activity) = Unit
            override fun onActivitySaveInstanceState(activity: Activity, state: Bundle) = Unit
            override fun onActivityDestroyed(activity: Activity) = Unit
        })
        val previous = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, failure ->
            runCatching { save("crash.txt", StabilityReport.stack("UNCAUGHT_EXCEPTION", failure.stackTrace, failure)) }
            // Preserve Android's normal crash handling. Never resume a corrupted process.
            previous?.uncaughtException(thread, failure)
        }
        scope.launch {
            if (Build.VERSION.SDK_INT >= 30) runCatching {
                val manager = application.getSystemService(ActivityManager::class.java)
                val exits = manager.getHistoricalProcessExitReasons(application.packageName, 0, 5)
                save("exits.txt", buildString {
                    appendLine("Android process exits (reason/status/importance; no raw descriptions)")
                    exits.forEach { appendLine("timestamp=${it.timestamp} reason=${it.reason} status=${it.status} importance=${it.importance} pssKiB=${it.pss} rssKiB=${it.rss}") }
                })
            }
            val detector = MainThreadStall()
            // No periodic wakeups while the UI is in the background.
            foreground.collectLatest { visible ->
                if (!visible) return@collectLatest
                while (isActive) {
                    delay(1_000)
                    val lastBeat = maxOf(heartbeat.get(), foregroundSince.get())
                    if (pending.get() && detector.shouldReport(SystemClock.uptimeMillis(), lastBeat, resumed.get() > 0, Debug.isDebuggerConnected())) {
                        runCatching { save("stall.txt", StabilityReport.stack("MAIN_THREAD_STALL", main.thread.stackTrace)) }
                    }
                    // One pending heartbeat at most; a blocked main thread cannot accumulate posts.
                    if (pending.compareAndSet(false, true)) {
                        heartbeat.set(SystemClock.uptimeMillis())
                        handler.post(beat)
                    }
                }
            }
        }
    }

    @Synchronized private fun save(name: String, text: String) {
        directory.mkdirs()
        File(directory, name).writeText("version=${BuildConfig.VERSION_NAME} code=${BuildConfig.VERSION_CODE} sdk=${Build.VERSION.SDK_INT}\n" + text.take(16_384))
    }

    @Synchronized fun report(): String {
        if (!initialized) return "No stability report recorded."
        return listOf("stall.txt", "crash.txt", "exits.txt").mapNotNull { name ->
            File(directory, name).takeIf { it.isFile }?.let { it.readText().take(17_000) }
        }.joinToString("\n\n").let { it + "\n\n" + ConnectionDiagnostics.report() }
    }
}
