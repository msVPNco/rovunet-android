package app.slipnet.presentation.common.components

import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.ui.Modifier
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextDirection
import androidx.compose.ui.platform.testTag
import app.slipnet.BuildConfig
import app.slipnet.R

@Composable
fun AboutDialogContent() {
    val resources = androidx.compose.ui.platform.LocalResources.current
    Column(modifier = Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Rovunet VPN v${BuildConfig.VERSION_NAME}", style = MaterialTheme.typography.titleMedium)
        Text(resources.getString(R.string.settings_one_app_for_dns_tunnels_ssh_tor_vless_reality_xhttp_hysteria2_and_7cb5ac))
        Text(resources.getString(R.string.settings_import_standard_links_wireguard_configuration_files_qr_codes_or_r_0f9de0))
        Text(resources.getString(R.string.settings_experimental_alpha_build_device_and_network_compatibility_testing_aba1ce), style = MaterialTheme.typography.bodySmall)
        TelegramCommunityLink()
        SupportRovunetCard()
        Text(resources.getString(R.string.amnezia_engine_notice), style = MaterialTheme.typography.bodySmall)
        Text(resources.getString(R.string.settings_open_source_acknowledgements_c41241), style = MaterialTheme.typography.titleSmall)
        LegalAttribution()
    }
}

@Composable
fun LegalAttribution() {
    CompositionLocalProvider(LocalLayoutDirection provides LayoutDirection.Ltr) {
        Text(androidx.compose.ui.res.stringResource(R.string.legal_attribution_english),
            modifier = Modifier.fillMaxWidth().testTag("legal-attribution"),
            style = MaterialTheme.typography.bodySmall.copy(fontSize = 11.sp, lineHeight = 16.sp,
                textDirection = TextDirection.Ltr, textAlign = TextAlign.Center),
            color = MaterialTheme.colorScheme.onSurface)
    }
}
