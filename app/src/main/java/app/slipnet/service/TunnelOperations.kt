package app.slipnet.service

import kotlinx.coroutines.sync.Mutex

/** Serializes process-wide native bridges across service instances. */
internal object TunnelOperations {
    val mutex = Mutex()
    var owner: Long = 0
}
