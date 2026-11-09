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
            OutlinedButton(onClick = onImport, modifier = Modifier.fillMaxWidth().testTag("free-proton-import")) { Text(stringResource(R.string.review02_proton_import)) }
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
internal fun PublicSourceDialog(repository: SubscriptionRepository, provider: PublicProviders.Provider? = null, onDismiss: () -> Unit, onImported: () -> Unit) {
    val presetName = if (provider == PublicProviders.Provider.MAHSA) stringResource(R.string.review02_mahsa) else stringResource(R.string.review02_anonymous)
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
                Text(stringResource(R.string.alpha17_public_consent), style = MaterialTheme.typography.bodySmall)
                Row { Checkbox(agreed, { agreed = it }, enabled = !busy); Text(stringResource(R.string.alpha17_public_ack), style = MaterialTheme.typography.bodySmall) }
            } else preview?.let { item ->
                Text(item.source.name, style = MaterialTheme.typography.titleMedium)
                Text(stringResource(R.string.alpha17_public_summary, item.profiles.size, item.unsupported, item.duplicates))
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
            TextButton(enabled = !busy && agreed && name.isNotBlank() && address.isNotBlank(), onClick = {
                execute { val ready = preview
                    if (ready == null) preview = repository.preparePublic(name, address, updateExisting = provider != null)
                    else { repository.apply(ready); onImported() }
                }
            }) { Text(stringResource(if (preview == null) R.string.subscription_preview else R.string.alpha17_public_import)) }
        }, dismissButton = { TextButton(onClick = { work?.cancel(); onDismiss() }) { Text(stringResource(android.R.string.cancel)) } })
}
