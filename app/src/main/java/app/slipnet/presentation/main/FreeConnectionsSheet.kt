package app.slipnet.presentation.main

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.slipnet.BuildConfig
import app.slipnet.R
import app.slipnet.data.subscription.SubscriptionPreview
import app.slipnet.data.subscription.SubscriptionRepository
import app.slipnet.data.subscription.PublicProviders
import kotlinx.coroutines.*

/** This catalog neither downloads nor connects until an explicit user action. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun FreeConnectionsSheet(repository: SubscriptionRepository, onTor: () -> Unit, onImport: () -> Unit, onDismiss: () -> Unit) {
    var showPublic by remember { mutableStateOf(false) }
    var provider by remember { mutableStateOf<PublicProviders.Provider?>(null) }
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text(stringResource(R.string.alpha17_free_title), style = MaterialTheme.typography.headlineSmall)
            Text(stringResource(R.string.alpha17_free_hint), style = MaterialTheme.typography.bodyMedium)
            if (BuildConfig.INCLUDE_TOR) OutlinedButton(onClick = onTor, modifier = Modifier.fillMaxWidth().testTag("free-tor")) { Text(stringResource(R.string.alpha17_free_tor)) }
            OutlinedButton(onClick = { provider = PublicProviders.Provider.PATTERNIHA; showPublic = true }, modifier = Modifier.fillMaxWidth().testTag("free-patterniha")) { Text(stringResource(R.string.review03_patterniha)) }
            HorizontalDivider()
            Text(stringResource(R.string.alpha17_public_title), style = MaterialTheme.typography.titleMedium)
            Text(stringResource(R.string.alpha17_public_consent), style = MaterialTheme.typography.bodySmall)
            OutlinedButton(onClick = { provider = PublicProviders.Provider.ANONYMOUS; showPublic = true }, modifier = Modifier.fillMaxWidth().testTag("free-anonymous")) { Text(stringResource(R.string.review02_anonymous)) }
            OutlinedButton(onClick = { provider = PublicProviders.Provider.MAHSA; showPublic = true }, modifier = Modifier.fillMaxWidth().testTag("free-mahsa")) { Text(stringResource(R.string.review02_mahsa)) }
            Button(onClick = { provider = null; showPublic = true }, modifier = Modifier.fillMaxWidth().testTag("public-source")) { Text(stringResource(R.string.alpha17_free_other)) }
            Spacer(Modifier.height(18.dp))
        }
    }
    if (showPublic) PublicSourceDialog(repository, provider = provider, onDismiss = { showPublic = false }, onImported = onDismiss)
}

@Composable
internal fun PublicSourceDialog(repository: SubscriptionRepository, provider: PublicProviders.Provider? = null,
    prepare: suspend (String, String, Boolean) -> SubscriptionPreview = { name,address,update -> repository.preparePublic(name,address,update) },
    onDismiss: () -> Unit, onImported: () -> Unit) {
    val presetName = stringResource(when (provider) {
        PublicProviders.Provider.MAHSA -> R.string.review02_mahsa
        PublicProviders.Provider.PATTERNIHA -> R.string.review03_patterniha
        else -> R.string.review02_anonymous
    })
    var name by remember(provider) { mutableStateOf(if (provider == null) "" else presetName) }
    var address by remember(provider) { mutableStateOf(provider?.address.orEmpty()) }
    var agreed by remember { mutableStateOf(false) }
    var preview by remember { mutableStateOf<SubscriptionPreview?>(null) }
    var failure by remember { mutableStateOf<String?>(null) }
    var busy by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()
    var work by remember { mutableStateOf<Job?>(null) }
    fun execute(action: suspend () -> Unit) {
        if (busy) return
        busy = true; failure = null
        work = scope.launch {
            try { action() }
            catch (e: CancellationException) { throw e }
            catch (e: Exception) { failure = e.message }
            finally { busy = false }
        }
    }
    AlertDialog(onDismissRequest = { work?.cancel(); onDismiss() },
        title = { Text(stringResource(R.string.alpha17_public_title)) },
        text = { Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            if (preview == null) {
                OutlinedTextField(name, { name = it.take(120) }, enabled = !busy, label = { Text(stringResource(R.string.subscription_name)) }, modifier = Modifier.fillMaxWidth())
                OutlinedTextField(address, { address = it }, enabled = !busy && provider == null, label = { Text(stringResource(R.string.subscription_url)) }, modifier = Modifier.fillMaxWidth())
                if (provider == PublicProviders.Provider.PATTERNIHA) Text(stringResource(R.string.review03_patterniha_hint), style = MaterialTheme.typography.bodySmall)
                Text(stringResource(R.string.alpha17_public_consent), style = MaterialTheme.typography.bodySmall)
                Row { Checkbox(agreed, { agreed = it }, enabled = !busy, modifier = Modifier.testTag("public-source-consent")); Text(stringResource(R.string.alpha17_public_ack), style = MaterialTheme.typography.bodySmall) }
            } else preview?.let { item ->
                Text(item.source.name, style = MaterialTheme.typography.titleMedium)
                Text(stringResource(R.string.alpha17_public_summary, item.profiles.size, item.unsupported, item.duplicates))
                PublicImportDetails(item)
                Text(stringResource(R.string.alpha17_public_refresh_warning), style = MaterialTheme.typography.bodySmall)
            }
            failure?.let { code -> Text(stringResource(when (code) {
                "PUBLIC_FEED_EMPTY" -> R.string.alpha17_public_empty
                "PUBLIC_FEED_LIMIT", "FEED_TOO_LARGE" -> R.string.alpha17_public_limit
                "SOURCE_EXISTS" -> R.string.subscription_source_exists
                "HTTPS_REQUIRED" -> R.string.subscription_https_required
                else -> if (provider != null) R.string.review02_provider_failed else R.string.subscription_failed
            }), color = MaterialTheme.colorScheme.error) }
            if (busy) LinearProgressIndicator(Modifier.fillMaxWidth())
        } },
        confirmButton = {
            TextButton(modifier = Modifier.testTag("public-source-confirm"), enabled = !busy && agreed && name.isNotBlank() && address.isNotBlank() && (preview == null || preview?.profiles?.isNotEmpty() == true), onClick = {
                execute { val ready = preview
                    if (ready == null) preview = prepare(name, address, provider != null)
                    else { repository.apply(ready); onImported() }
                }
            }) { Text(stringResource(if (preview == null) R.string.subscription_preview else R.string.alpha17_public_import)) }
        }, dismissButton = { TextButton(onClick = { work?.cancel(); onDismiss() }) { Text(stringResource(android.R.string.cancel)) } })
}

@Composable
internal fun PublicImportDetails(item: SubscriptionPreview) {
    if (item.profiles.isEmpty()) Text(stringResource(R.string.alpha17_public_empty), color = MaterialTheme.colorScheme.error)
    for (warning in item.importWarnings) Text(stringResource(if (warning == "REVIEW03_JSON_CONNECTIONS_ONLY") R.string.review03_json_connections else R.string.review03_json_unsupported), style = MaterialTheme.typography.bodySmall)
    for (stat in item.publicStats) {
        val label = when (stat.protocol) {
            "OTHER" -> stringResource(R.string.review03_other)
            "SS" -> stringResource(R.string.review02_shadowsocks)
            "VMESS" -> stringResource(R.string.review03_vmess)
            "TROJAN" -> stringResource(R.string.review03_trojan)
            else -> stat.protocol
        }
        Text(stringResource(R.string.review03_protocol_summary, label, stat.accepted, stat.rejected), style = MaterialTheme.typography.bodySmall)
        for ((reason, count) in stat.reasons) {
            val label = stringResource(when (reason) {
                app.slipnet.data.subscription.PublicFeed.Reason.FORMAT -> R.string.review03_reason_format
                app.slipnet.data.subscription.PublicFeed.Reason.PROTOCOL -> R.string.review03_reason_protocol
                app.slipnet.data.subscription.PublicFeed.Reason.TRANSPORT -> R.string.review03_reason_transport
                app.slipnet.data.subscription.PublicFeed.Reason.TLS -> R.string.review03_reason_tls
                app.slipnet.data.subscription.PublicFeed.Reason.OPTIONS -> R.string.review03_reason_options
                app.slipnet.data.subscription.PublicFeed.Reason.PROTECTED -> R.string.review03_reason_protected
            })
            Text(stringResource(R.string.review03_reason_count, label, count), style = MaterialTheme.typography.bodySmall)
        }
    }
}
