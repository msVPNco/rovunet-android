package app.slipnet.domain.model

/** Counts executed probes, never treating an early exit as failed unsent probes. */
data class ProbeVerification(val passed: Int, val attempted: Int?)
