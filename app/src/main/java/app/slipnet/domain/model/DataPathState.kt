package app.slipnet.domain.model

/** Evidence about an HTTPS request through the current proxy, never a connection gate. */
enum class DataPathState { UNTESTED, CHECKING, VERIFIED, UNKNOWN }

/** Whether the home screen has anything to show for [state] right now. */
fun dataPathNoteVisible(state: DataPathState, passFresh: Boolean): Boolean =
    (state == DataPathState.VERIFIED && passFresh) || state == DataPathState.UNKNOWN
