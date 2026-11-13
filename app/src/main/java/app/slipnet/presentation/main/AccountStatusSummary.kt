package app.slipnet.presentation.main

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.slipnet.R
import app.slipnet.data.subscription.*
import app.slipnet.presentation.theme.collectionTones
import app.slipnet.presentation.common.components.MarqueeText
import java.text.NumberFormat

/** A compact addition inside the existing collection surface, shared by all its paths. */
@Composable
internal fun AccountStatusSummary(view: AccountStatusView) {
    val status = view.status
    val color = when (view.severity) {
        AccountSeverity.CRITICAL -> MaterialTheme.colorScheme.error
        AccountSeverity.WARNING -> if (MaterialTheme.colorScheme.background.luminance() > .5f) Color(0xFF885000) else Color(0xFFFFCF70)
        AccountSeverity.UNKNOWN -> collectionTones.headerText.copy(alpha = .65f)
        AccountSeverity.NORMAL -> collectionTones.headerText.copy(alpha = .85f)
    }
    val message = when {
        !view.trustworthy -> stringResource(when {
            view.error.isNotEmpty() || status == null -> R.string.account_status_unavailable
            status.metering == "partial" -> R.string.account_status_partial
            status.metering == "unavailable" -> R.string.account_status_waiting
            else -> R.string.account_status_stale
        })
        status?.state == "expired" || view.remainingMillis == 0L -> stringResource(R.string.account_status_expired)
        status?.state == "limit_reached" || status?.remainingBytes == 0L -> stringResource(R.string.account_status_limit_reached)
        status != null -> {
            val volume = if (!status.volumeKnown) stringResource(R.string.subscription_volume_unknown) else (status.remainingBytes ?: status.limitBytes)?.let { bytes ->
                val gib = bytes >= 1024L * 1024 * 1024
                val divisor = if (gib) 1024.0 * 1024 * 1024 else 1024.0 * 1024
                val number = NumberFormat.getNumberInstance().apply { maximumFractionDigits = 1 }.format(bytes / divisor)
                val label = if (status.usedBytes != null) R.string.account_status_remaining_volume
                    else if (status.usageLowerBound != null) R.string.subscription_limit_partial_usage else R.string.subscription_limit_unknown_usage
                stringResource(label, number, if (gib) "GiB" else "MiB")
            } ?: stringResource(R.string.account_status_unlimited_volume)
            val time = if (!status.expiryKnown) stringResource(R.string.subscription_expiry_unknown) else view.remainingMillis?.let { millis ->
                when {
                    millis > 86_400_000 -> stringResource(R.string.account_status_days, (millis + 86_399_999) / 86_400_000)
                    millis > 3_600_000 -> stringResource(R.string.account_status_hours, (millis + 3_599_999) / 3_600_000)
                    else -> stringResource(R.string.account_status_minutes, ((millis + 59_999) / 60_000).coerceAtLeast(1))
                }
            } ?: stringResource(R.string.account_status_no_expiry)
            stringResource(R.string.account_status_summary, volume, time)
        }
        else -> stringResource(R.string.account_status_unavailable)
    }
    Column(verticalArrangement = Arrangement.spacedBy(4.dp), modifier = Modifier.fillMaxWidth()) {
        MarqueeText(message, style = MaterialTheme.typography.labelSmall, color = color)
        if (status?.providerReported == true) MarqueeText(stringResource(R.string.subscription_provider_snapshot,
            java.text.DateFormat.getDateTimeInstance(java.text.DateFormat.SHORT, java.text.DateFormat.SHORT).format(java.util.Date(status.generatedAt))),
            style = MaterialTheme.typography.labelSmall, color = collectionTones.headerText.copy(alpha = .65f))
        if (view.trustworthy && status != null && (status.metering == "complete" || status.providerReported) && status.limitBytes != null && status.remainingBytes != null && status.state == "active") {
            LinearProgressIndicator(progress = { (requireNotNull(status.remainingBytes).toDouble() / status.limitBytes).coerceIn(0.0, 1.0).toFloat() },
                modifier = Modifier.fillMaxWidth().height(3.dp), color = color,
                trackColor = collectionTones.headerText.copy(alpha = .12f))
        }
    }
}
