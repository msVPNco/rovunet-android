package app.slipnet.presentation.scanner

import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import app.slipnet.R

@Composable
internal fun scannerTransportName(name: String): String =
    if (name == "Both") stringResource(R.string.scanner_both) else name

@Composable
internal fun scannerCountryName(code: String): String {
    val locale = LocalConfiguration.current.locales[0]
    return java.util.Locale("", code.uppercase(java.util.Locale.ROOT)).getDisplayCountry(locale)
}

@Composable
internal fun scannerPhaseName(phase: String?): String = when {
    phase.isNullOrBlank() -> stringResource(R.string.scanner_testing)
    phase.contains("DNS", ignoreCase = true) -> "DNS"
    phase.contains("SSH", ignoreCase = true) -> "SSH"
    phase.contains("TLS", ignoreCase = true) -> "TLS"
    phase.contains("HTTP", ignoreCase = true) -> "HTTP"
    else -> stringResource(R.string.scanner_testing)
}

/** Backend details remain in local diagnostics; UI never needs raw exception text. */
internal fun scannerErrorText(context: android.content.Context, error: String): String {
    val key = when {
        error.startsWith("Too many IPs") -> R.string.scanner_error_too_many
        error.startsWith("No IP ranges selected") -> R.string.scanner_error_select_ranges
        error.startsWith("No valid IP") || error.startsWith("No CIDR") -> R.string.scanner_error_ranges
        error == "No resolvers to scan" -> R.string.scanner_error_no_resolvers
        error == "Test domain is required" -> R.string.scanner_error_domain
        error.startsWith("Prism requires") || error == "No profile loaded" -> R.string.scanner_error_profile
        error.startsWith("Per-probe timeout") -> R.string.scanner_error_probe_timeout
        error.startsWith("Failed to parse file") -> R.string.scanner_error_file
        error.contains("timeout", true) || error.contains("timed out", true) -> R.string.scanner_error_timeout
        error.contains("auth", true) -> R.string.scanner_error_auth
        else -> R.string.scanner_error_scan
    }
    return context.getString(key)
}
