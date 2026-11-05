import app.slipnet.core.MainThreadStall
import app.slipnet.core.StabilityReport

fun main() {
    val detector = MainThreadStall(5_000)
    check(!detector.shouldReport(9_000, 1_000, false, false))
    check(!detector.shouldReport(9_000, 1_000, true, true))
    check(!detector.shouldReport(5_999, 1_000, true, false))
    check(detector.shouldReport(6_000, 1_000, true, false))
    check(!detector.shouldReport(60_000, 1_000, true, false))
    check(!detector.shouldReport(10_000, 12_000, true, false))
    check(detector.shouldReport(17_000, 12_000, true, false))
    val secret = "private-user:password@192.0.2.5"
    val exception = IllegalStateException(secret, RuntimeException(secret))
    val frames = arrayOf(StackTraceElement("app.slipnet.SomeClass", "work", "/private/$secret", 12))
    val report = StabilityReport.stack("UNCAUGHT_EXCEPTION", frames, exception)
    check(!report.contains(secret) && !report.contains("/private/") && !report.contains("192.0.2.5"))
    check(report.contains("java.lang.IllegalStateException") && report.contains("SomeClass.work:12"))
    check(StabilityReport.stack("MAIN_THREAD_STALL", Array(200) { frames[0] }).lineSequence().count() <= 67)
    println("PASS foreground-only stall detection, debugger/deep-sleep guards, one report per stall, bounded symbol-only redaction")
}
