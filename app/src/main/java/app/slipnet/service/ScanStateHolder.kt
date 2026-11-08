package app.slipnet.service

import app.slipnet.util.AppLog
import java.util.UUID
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineExceptionHandler
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/** A run outlives its screen. A replacement waits for cancelled workers to release their native clients. */
internal object ScanStateHolder {
    private val cleanupScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    @Volatile var current = ScanRun(null)
        private set

    @Synchronized fun begin(): ScanRun {
        val previous = current
        previous.stop()
        return ScanRun(previous.drained).also { current = it; app.slipnet.util.ScanDiagnostics.begin(it.id) }
    }

    fun drain(previous: Job?, worker: Job): Job = cleanupScope.launch {
        previous?.join()
        worker.join()
    }
}

internal enum class ScanPhase { DNS, E2E }

internal class ScanRun(private val predecessor: Job?) {
    val id: String = UUID.randomUUID().toString()
    private val parent = SupervisorJob()
    private val errors = CoroutineExceptionHandler { _, error ->
        AppLog.e("DnsScanner", "Scan worker failed: ${error.javaClass.simpleName}")
    }
    private val scope = CoroutineScope(parent + Dispatchers.IO + errors)
    val drained = ScanStateHolder.drain(predecessor, parent)
    private val mutableState = MutableStateFlow(ScanServiceState())
    val state = mutableState.asStateFlow()
    private val workers = mutableMapOf<ScanPhase, Job>()
    private val phaseDrains = mutableMapOf<ScanPhase, Job>()

    @Synchronized fun update(transform: (ScanServiceState) -> ScanServiceState) {
        if (parent.isActive) mutableState.value = transform(mutableState.value)
    }

    /** At most one worker per phase uses the native scanner at a time. */
    @Synchronized fun launch(phase: ScanPhase, work: suspend CoroutineScope.() -> Unit): Job {
        val before = phaseDrains[phase] ?: predecessor
        workers[phase]?.cancel()
        val worker = scope.launch(start = CoroutineStart.LAZY) {
            before?.join()
            app.slipnet.util.ScanDiagnostics.working(id)
            work()
        }
        workers[phase] = worker
        phaseDrains[phase] = ScanStateHolder.drain(before, worker)
        worker.invokeOnCompletion { error ->
            synchronized(this) {
                if (workers[phase] === worker) {
                    workers.remove(phase)
                    if (error != null && error !is CancellationException) stop() else markFinished(phase)
                }
            }
        }
        worker.start()
        return worker
    }

    @Synchronized fun stopPhase(phase: ScanPhase) {
        workers[phase]?.cancel()
        markFinished(phase)
    }

    private fun markFinished(phase: ScanPhase) = update {
        val next = when (phase) {
            ScanPhase.DNS -> it.copy(isScanning = false)
            ScanPhase.E2E -> it.copy(isE2eRunning = false, e2eActiveResolvers = emptyMap())
        }
        if (!next.active) app.slipnet.util.ScanDiagnostics.finish(id)
        next
    }

    @Synchronized fun stop(clearResults: Boolean = false) {
        parent.cancel()
        mutableState.value = if (clearResults) ScanServiceState() else mutableState.value.copy(
            isScanning = false, isE2eRunning = false, e2eActiveResolvers = emptyMap()
        )
        drained.invokeOnCompletion { app.slipnet.util.ScanDiagnostics.finish(id) }
    }
}
