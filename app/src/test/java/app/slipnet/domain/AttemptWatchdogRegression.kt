package app.slipnet.domain

import app.slipnet.domain.model.AttemptWatchdog
import kotlinx.coroutines.*
import java.util.concurrent.atomic.AtomicInteger

fun attemptWatchdogRegression() = runBlocking {
    val events = AtomicInteger()
    val watch = AttemptWatchdog(this)
    watch.arm(50, { true }) { events.incrementAndGet() }
    delay(80)
    check(events.get() == 1)
    watch.arm(40, { true }) { events.addAndGet(10) }
    watch.cancel()
    delay(70)
    check(events.get() == 1)
    watch.arm(40, { true }) { events.addAndGet(100) }
    watch.arm(70, { false }) { events.addAndGet(1000) }
    delay(100)
    check(events.get() == 1)
    watch.cancel()
    val pauseStarted = System.nanoTime()
    var waiting = true
    var accumulated = 0L
    watch.arm(80, { true }, { if (waiting) System.nanoTime() - pauseStarted else accumulated }) { events.incrementAndGet() }
    delay(160)
    check(events.get() == 1)
    accumulated = System.nanoTime() - pauseStarted
    waiting = false
    delay(140)
    check(events.get() == 2)
    watch.cancel()
    println("PASS attempt watchdog expiration, cancellation, replacement and completed-attempt isolation")
}
