package app.slipnet.core

/** No exception messages, file paths, endpoints, user names or thread names. */
object StabilityReport {
    fun stack(kind: String, frames: Array<StackTraceElement>, error: Throwable? = null): String = buildString {
        appendLine("Rovunet stability report v1")
        appendLine("event=$kind")
        error?.let { appendLine("exception=${it.javaClass.name}") }
        for (frame in frames.take(64)) {
            appendLine("at ${frame.className}.${frame.methodName}:${frame.lineNumber}")
        }
    }
}

/** Uptime excludes deep sleep. A stall is reported once, then rearmed by a heartbeat. */
class MainThreadStall(private val thresholdMs: Long = 5_000) {
    private var lastReportedHeartbeat: Long? = null
    fun shouldReport(now: Long, heartbeat: Long, foreground: Boolean, debugger: Boolean): Boolean {
        if (!foreground || debugger || now - heartbeat < thresholdMs || now < heartbeat) return false
        if (lastReportedHeartbeat == heartbeat) return false
        lastReportedHeartbeat = heartbeat
        return true
    }
}
