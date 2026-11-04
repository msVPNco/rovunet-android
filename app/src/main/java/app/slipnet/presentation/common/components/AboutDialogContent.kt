package app.slipnet.presentation.common.components

import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.ui.Modifier
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.unit.dp
import app.slipnet.BuildConfig

@Composable
fun AboutDialogContent() {
    Column(modifier = Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Rovunet VPN v${BuildConfig.VERSION_NAME}", style = MaterialTheme.typography.titleMedium)
        Text("One app for DNS tunnels, SSH, Tor, VLESS REALITY, XHTTP, Hysteria2 and WireGuard.")
        Text("Import standard links, WireGuard configuration files, QR codes or Rovunet profiles.")
        Text("Experimental alpha build. Device and network compatibility testing is ongoing.", style = MaterialTheme.typography.bodySmall)
        TelegramCommunityLink()
        SupportRovunetCard()
        Text("Open-source acknowledgements", style = MaterialTheme.typography.titleSmall)
        Text("Based on SlipNet 2.5.6 Full by its original contributors. Rovunet is an independent fork, not an official SlipNet release. Original copyright and license notices are retained. Xray-core is distributed under MPL-2.0.", style = MaterialTheme.typography.bodySmall)
    }
}
