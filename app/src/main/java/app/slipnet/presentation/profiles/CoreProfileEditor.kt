package app.slipnet.presentation.profiles

import app.slipnet.presentation.theme.rv
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Check
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
fun CoreProfileEditor(
    state: EditProfileUiState,
    onField: (String, String) -> Unit,
    onName: (String) -> Unit,
    onSave: () -> Unit,
    onBack: () -> Unit,
    snackbar: SnackbarHostState
) {
    val form = state.coreForm
    val title = when (form?.kind) {
        "wireguard" -> "WireGuard"
        "hysteria2" -> "Hysteria2"
        else -> "VLESS • ${if (form?.get("type") == "xhttp") "XHTTP" else "TCP"}"
    }
    Scaffold(
        topBar = { TopAppBar(title = { Text(rv(title)) }, navigationIcon = {
            IconButton(onClick = onBack) { Icon(Icons.Default.ArrowBack, "Back") }
        }, actions = {
            IconButton(onClick = onSave, enabled = !state.isSaving && form != null) { Icon(Icons.Default.Check, "Save profile") }
        }) },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(padding).verticalScroll(rememberScrollState()).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)) {
            if (form == null) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_unable_to_load_core_settings_7aef2a)); return@Column }
            if (state.coreAsCopy) Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_saving_creates_a_new_profile_the_original_websocket_profile_is_unchanged_6d1658))
            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_settings_must_match_your_server_changing_the_transport_here_does_not_change_the_server_43046f), style = MaterialTheme.typography.bodySmall)
            OutlinedTextField(value = state.name, onValueChange = onName, label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_profile_name_d36632)) }, modifier = Modifier.fillMaxWidth(), singleLine = true)

            @Composable fun field(key: String, label: String, secret: Boolean = false, multiline: Boolean = false) {
                var reveal by remember(key) { mutableStateOf(false) }
                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr), value = form[key], onValueChange = { onField(key, it) }, label = { Text(rv(label)) },
                    modifier = Modifier.fillMaxWidth(), singleLine = !multiline,
                    visualTransformation = if (secret && !reveal) PasswordVisualTransformation() else VisualTransformation.None,
                    trailingIcon = if (secret) { { TextButton(onClick = { reveal = !reveal }) { Text(rv(if (reveal) "Hide" else "Show")) } } } else null)
            }
            @Composable fun choices(key: String, label: String, values: List<String>, default: String = "") {
                Text(label, style = MaterialTheme.typography.titleSmall)
                FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    values.forEach { value ->
                        FilterChip(selected = form[key].ifBlank { default } == value,
                            onClick = { onField(key, value) }, label = { Text(value.ifEmpty { "None" }) })
                    }
                }
            }

            if (form.kind == "wireguard") {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_interface_c26b3e), style = MaterialTheme.typography.titleMedium)
                field("privatekey", "Private key", secret = true)
                field("address", "Interface addresses (CIDR, comma separated)")
                field("dns", "DNS servers (optional)")
                field("mtu", "MTU (optional)")
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_peer_f8e772), style = MaterialTheme.typography.titleMedium)
                field("publickey", "Server public key")
                field("presharedkey", "Preshared key (optional)", secret = true)
                field("endpoint", "Endpoint (host:port or [IPv6]:port)")
                field("allowedips", "Allowed IPs (CIDR, comma separated)")
                field("persistentkeepalive", "Persistent keepalive (seconds, optional)")
            } else {
                field("server", "Server address")
                field("port", "Server port")
                field("auth", if (form.kind == "vless") "UUID" else "Authentication password", secret = true)
                if (form.kind == "vless") {
                    choices("type", "Transport", listOf("tcp", "xhttp"), "tcp")
                    choices("security", "Security", listOf("reality", "tls", "none"), "none")
                    if (form["security"] != "none") {
                        field("sni", "TLS server name (SNI)")
                        field("fp", "TLS fingerprint (e.g. chrome)")
                        if (form["security"] == "tls") field("alpn", "ALPN (optional, comma separated)")
                    }
                    if (form["security"] == "reality") {
                        field("pbk", "REALITY public key")
                        field("sid", "REALITY short ID (hex)")
                        field("spx", "SpiderX path (optional)")
                    }
                    if (form["type"] == "xhttp") {
                        field("host", "HTTP Host (optional)")
                        field("path", "XHTTP path")
                        choices("mode", "XHTTP mode", listOf("auto", "packet-up", "stream-up", "stream-one"), "auto")
                        field("extra", "XHTTP extra JSON (optional)", multiline = true)
                    } else if (form["security"] != "none") {
                        choices("flow", "Flow", listOf("", "xtls-rprx-vision"))
                    }
                } else {
                    field("sni", "TLS server name (SNI)")
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_tls_certificate_verification_is_enabled_de2de3), style = MaterialTheme.typography.bodySmall)
                    choices("obfs", "Obfuscation", listOf("", "salamander"))
                    if (form["obfs"] == "salamander") field("obfs-password", "Salamander password", secret = true)
                }
            }
            Button(onClick = onSave, enabled = !state.isSaving, modifier = Modifier.fillMaxWidth()) {
                Text(rv(if (state.isSaving) "Saving…" else "Save profile"))
            }
        }
    }
}
