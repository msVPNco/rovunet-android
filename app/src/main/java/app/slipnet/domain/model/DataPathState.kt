package app.slipnet.domain.model

/** Evidence about an HTTPS request through the current proxy, never a connection gate. */
enum class DataPathState { UNTESTED, CHECKING, VERIFIED, UNKNOWN }
