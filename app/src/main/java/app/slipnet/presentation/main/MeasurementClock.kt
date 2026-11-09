package app.slipnet.presentation.main

import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import app.slipnet.domain.model.PingResult

/** A new measurement refreshes the clock immediately, without waiting for the minute tick. */
@Composable
internal fun rememberMeasurementClock(tick: Long, results: Map<Long,PingResult>): Long =
    remember(tick, results) { System.currentTimeMillis() }
