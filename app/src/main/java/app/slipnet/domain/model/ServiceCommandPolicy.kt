package app.slipnet.domain.model

/** A queued UI request has not yet taken ownership of an existing Android service.
 * Stops target the last CONNECT delivered to that instance, not the manager's newest queued request.
 */
object ServiceCommandPolicy {
    fun acceptsStop(expected: Long, delivered: Long): Boolean = expected > 0 && expected == delivered
    fun mayStopService(owner: Long, delivered: Long): Boolean = owner == delivered
}
