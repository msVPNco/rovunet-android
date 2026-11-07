package app.slipnet.domain.model

/** Evidence about an HTTPS request through the current proxy, never a connection gate. */
enum class DataPathState { UNTESTED, CHECKING, VERIFIED, UNKNOWN }

/** The deadline belongs to the probe result, never to a screen's composition lifetime. */
fun dataPathNoteVisible(state: DataPathState, expiresAtElapsedMs: Long, nowElapsedMs: Long): Boolean =
    state == DataPathState.VERIFIED && nowElapsedMs < expiresAtElapsedMs
