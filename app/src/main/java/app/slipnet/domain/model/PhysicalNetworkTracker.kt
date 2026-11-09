package app.slipnet.domain.model

/** Events about a secondary network do not invalidate the selected physical route. */
class PhysicalNetworkTracker(initial: String?) {
    private var initialized = initial != null
    private var current = initial
    fun update(selected: String?): Boolean {
        if (!initialized) { current = selected; initialized = selected != null; return false }
        if (selected == current) return false
        current = selected
        return true
    }
}
