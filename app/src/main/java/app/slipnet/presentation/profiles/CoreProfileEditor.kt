package app.slipnet.presentation.profiles

import app.slipnet.presentation.theme.rv
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
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
        "trojan" -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.review03_trojan)
        "vmess" -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.review03_vmess)
        "ss" -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.review02_shadowsocks)
        "wireguard" -> "WireGuard"
        "hysteria2" -> "Hysteria2"
        else -> "VLESS • ${form?.get("type").orEmpty().ifBlank { "tcp" }.uppercase()}"
    }
    Scaffold(
        topBar = { TopAppBar(title = { Text(rv(title)) }, navigationIcon = {
            IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, androidx.compose.ui.res.stringResource(app.slipnet.R.string.recovery_back)) }
        }, actions = {
            IconButton(onClick = onSave, enabled = !state.isSaving && form != null) { Icon(Icons.Default.Check, androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_save_1509f5)) }
        }) },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(padding).verticalScroll(rememberScrollState()).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)) {
            if (form == null) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_unable_to_load_core_settings_7aef2a)); return@Column }
            if (state.coreAsCopy) Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_saving_creates_a_new_profile_the_original_websocket_profile_is_unchanged_6d1658))
            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_settings_must_match_your_server_changing_the_transport_here_does_not_change_the_server_43046f), style = MaterialTheme.typography.bodySmall)
            OutlinedTextField(value = state.name, onValueChange = onName, label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_profile_name_d36632)) }, modifier = Modifier.fillMaxWidth(), singleLine = true)

            @Composable fun field(key: String, @androidx.annotation.StringRes label: Int, secret: Boolean = false, multiline: Boolean = false) {
                var reveal by remember(key) { mutableStateOf(false) }
                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr), value = form[key], onValueChange = { onField(key, it) }, label = { Text(androidx.compose.ui.res.stringResource(label)) },
                    modifier = Modifier.fillMaxWidth(), singleLine = !multiline,
                    visualTransformation = if (secret && !reveal) PasswordVisualTransformation() else VisualTransformation.None,
                    trailingIcon = if (secret) { { TextButton(onClick = { reveal = !reveal }) { Text(androidx.compose.ui.res.stringResource(if (reveal) app.slipnet.R.string.rv_hide_ac20a5 else app.slipnet.R.string.rv_show_0df6f1)) } } } else null)
            }
            @Composable fun choices(key: String, @androidx.annotation.StringRes label: Int, values: List<String>, default: String = "") {
                Text(androidx.compose.ui.res.stringResource(label), style = MaterialTheme.typography.titleSmall)
                FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    values.forEach { value ->
                        FilterChip(selected = form[key].ifBlank { default } == value,
                            onClick = { onField(key, value) }, label = { Text(if (value.isEmpty() || value == "none") androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_none) else if (value == "auto") androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_tcp_auto) else value) })
                    }
                }
            }

            if (form.kind == "wireguard") {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_interface_c26b3e), style = MaterialTheme.typography.titleMedium)
                field("privatekey", app.slipnet.R.string.rv_private_key_477bf9, secret = true)
                field("address", app.slipnet.R.string.rv_interface_addresses_cidr_comma_separated_3cdfb2)
                field("dns", app.slipnet.R.string.rv_dns_servers_optional_4e8d1b)
                field("mtu", app.slipnet.R.string.rv_mtu_optional_3fa48b)
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_peer_f8e772), style = MaterialTheme.typography.titleMedium)
                field("publickey", app.slipnet.R.string.rv_server_public_key_3bff38)
                field("presharedkey", app.slipnet.R.string.rv_preshared_key_optional_580dd0, secret = true)
                field("endpoint", app.slipnet.R.string.rv_endpoint_host_port_or_ipv6_port_d604e3)
                field("allowedips", app.slipnet.R.string.rv_allowed_ips_cidr_comma_separated_2a497e)
                field("persistentkeepalive", app.slipnet.R.string.rv_persistent_keepalive_seconds_optional_56c33a)
            } else {
                field("server", app.slipnet.R.string.rv_server_address_8be897)
                field("port", app.slipnet.R.string.rv_server_port_426f91)
                field("auth", if (form.kind in setOf("vless", "vmess")) app.slipnet.R.string.editor_uuid else app.slipnet.R.string.editor_core_auth_password, secret = true)
                if (form.kind in setOf("vless", "trojan", "vmess")) {
                    choices("type", app.slipnet.R.string.rv_transport_aaead4, if (form.kind == "vless") listOf("tcp", "xhttp", "ws", "grpc", "httpupgrade", "kcp") else listOf("tcp", "ws", "grpc", "httpupgrade"), "tcp")
                    choices("security", app.slipnet.R.string.editor_security_label, when (form.kind) { "trojan" -> listOf("tls"); "vmess" -> listOf("tls", "none"); else -> listOf("reality", "tls", "none") }, "none")
                    if (form.kind == "vmess") choices("cipher", app.slipnet.R.string.review03_vmess_cipher, listOf("auto", "aes-128-gcm", "chacha20-poly1305"), "auto")
                    if (form["security"] != "none") {
                        field("sni", app.slipnet.R.string.rv_tls_server_name_sni_719c47)
                        field("fp", app.slipnet.R.string.rv_tls_fingerprint_e_g_chrome_a19b6a)
                        if (form["security"] == "tls") field("alpn", app.slipnet.R.string.rv_alpn_optional_comma_separated_5969cd)
                    }
                    if (form["security"] == "reality") {
                        field("pbk", app.slipnet.R.string.rv_reality_public_key_8cf5de)
                        field("sid", app.slipnet.R.string.rv_reality_short_id_hex_a1d63e)
                        field("spx", app.slipnet.R.string.rv_spiderx_path_optional_5fb3c2)
                    }
                    if (form["type"] in setOf("ws", "httpupgrade")) {
                        field("host", app.slipnet.R.string.rv_http_host_optional_2ba28f)
                        field("path", app.slipnet.R.string.rv_xhttp_path_12ea5e)
                        if (form["type"] == "ws") {
                            field("ed", app.slipnet.R.string.review03_early_data)
                            field("eh", app.slipnet.R.string.review03_early_header)
                        }
                    } else if (form["type"] == "grpc") {
                        field("serviceName", app.slipnet.R.string.review03_grpc_service)
                        field("authority", app.slipnet.R.string.review03_grpc_authority)
                        choices("mode", app.slipnet.R.string.rv_transport_aaead4, listOf("gun", "multi"), "gun")
                    } else if (form["type"] == "xhttp") {
                        field("host", app.slipnet.R.string.rv_http_host_optional_2ba28f)
                        field("path", app.slipnet.R.string.rv_xhttp_path_12ea5e)
                        choices("mode", app.slipnet.R.string.rv_xhttp_mode_289518, listOf("auto", "packet-up", "stream-up", "stream-one"), "auto")
                        field("extra", app.slipnet.R.string.rv_xhttp_extra_json_optional_c14cfc, multiline = true)
                    } else if (form.kind == "vless" && form["security"] != "none") {
                        choices("flow", app.slipnet.R.string.editor_flow_label, listOf("", "xtls-rprx-vision"))
                    }
                } else if (form.kind == "ss") {
                    choices("method", app.slipnet.R.string.review02_ss_method,
                        listOf("aes-128-gcm", "aes-256-gcm", "chacha20-ietf-poly1305", "xchacha20-ietf-poly1305"))
                } else {
                    field("sni", app.slipnet.R.string.rv_tls_server_name_sni_719c47)
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_tls_certificate_verification_is_enabled_de2de3), style = MaterialTheme.typography.bodySmall)
                    choices("obfs", app.slipnet.R.string.rv_obfuscation_04d69e, listOf("", "salamander"))
                    if (form["obfs"] == "salamander") field("obfs-password", app.slipnet.R.string.rv_salamander_password_967e15, secret = true)
                }
            }
            Button(onClick = onSave, enabled = !state.isSaving, modifier = Modifier.fillMaxWidth()) {
                Text(androidx.compose.ui.res.stringResource(if (state.isSaving) app.slipnet.R.string.rv_saving_23e392 else app.slipnet.R.string.rv_save_profile_0c8209))
            }
        }
    }
}
