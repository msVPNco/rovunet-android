package app.slipnet.presentation.theme

import android.content.res.Resources
import app.slipnet.R

/** Technical backend messages stay out of ordinary UI; known actionable categories use current resources. */
object UiErrors {
    fun text(resources: Resources, code: String): String {
        RovunetStrings.resourceId(code)?.let { return resources.getString(it) }
        val key = when {
            code == "PROBE_BUSY" -> R.string.probe_busy
            code == "PROBE_CLEANUP" -> R.string.probe_cleanup
            code == "PROBE_CHANGED" -> R.string.probe_changed
            code == "PROBE_NETWORK_CHANGED" -> R.string.review02_network_changed
            code == "HTTPS_FAILED" -> R.string.live_probe_failed
            code == "DATABASE_UNAVAILABLE" -> R.string.ui_database_unavailable
            code.contains("Unsupported version", true) || code.contains("Unsupported collection version", true) -> R.string.ui_error_update
            code.contains("import", true) || code.contains("decode", true) -> R.string.ui_error_import
            code.contains("timed out", true) || code.contains("timeout", true) -> R.string.ui_error_timeout
            else -> R.string.ui_error_general
        }
        return resources.getString(key)
    }
}
