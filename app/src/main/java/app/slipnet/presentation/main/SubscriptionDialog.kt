package app.slipnet.presentation.main

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import app.slipnet.R
import app.slipnet.data.subscription.*
import kotlinx.coroutines.*

@Composable
internal fun SubscriptionDialog(repository: SubscriptionRepository, initialUrl: String?, initialId: String?, onDisconnect: (() -> Unit)? = null, onDismiss: () -> Unit) {
    val sources by repository.observe().collectAsState(initial = emptyList())
    val connection by repository.connectionState.collectAsState()
    val hosts by produceState<Map<String, String>>(emptyMap(), sources) {
        value = withContext(Dispatchers.IO) { sources.associate { it.id to repository.host(it) } }
    }
    var name by remember { mutableStateOf("") }
    var url by remember { mutableStateOf(initialUrl ?: "") }
    var editing by remember { mutableStateOf<String?>(null) }
    var automatic by remember { mutableStateOf(false) }
    var visible by remember { mutableStateOf(false) }
    var preview by remember { mutableStateOf<SubscriptionPreview?>(null) }
    var error by remember { mutableStateOf<String?>(null) }
    var busy by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()
    var job by remember { mutableStateOf<Job?>(null) }
    fun execute(action: suspend () -> Unit) {
        if (busy) return
        busy = true; error = null
        job = scope.launch {
            try { action() }
            catch (e: CancellationException) { throw e }
            catch (e: Exception) { error = e.message }
            finally { busy = false }
        }
    }
    LaunchedEffect(initialId, sources.map { it.id }) {
        if (editing == null && initialId != null) sources.firstOrNull { it.id == initialId }?.let {
            name = it.name; url = runCatching { repository.sourceUrl(it) }.getOrDefault(""); editing = it.id; automatic = it.autoUpdate
        }
    }
    AlertDialog(onDismissRequest = { job?.cancel(); onDismiss() },
        title = { Text(stringResource(R.string.subscriptions_title)) },
        text = {
            Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(stringResource(R.string.subscription_hint), style = MaterialTheme.typography.bodySmall)
                if (!connection.isDisconnected && onDisconnect != null) TextButton(
                    enabled = connection !is app.slipnet.domain.model.ConnectionState.Disconnecting,
                    onClick = onDisconnect) { Text(stringResource(R.string.ui_disconnect)) }
                OutlinedTextField(name, { name = it.take(120) }, enabled = !busy, label = { Text(stringResource(R.string.subscription_name)) }, modifier = Modifier.fillMaxWidth())
                OutlinedTextField(url, { url = it }, enabled = !busy, label = { Text(stringResource(R.string.subscription_url)) },
                    visualTransformation = if (visible) VisualTransformation.None else PasswordVisualTransformation(), modifier = Modifier.fillMaxWidth(),
                    trailingIcon = { TextButton(onClick = { visible = !visible }) { Text(stringResource(if (visible) R.string.rv_hide_ac20a5 else R.string.rv_show_0df6f1)) } })
                Row { Checkbox(automatic, { automatic = it }, enabled = !busy); Text(stringResource(R.string.subscription_automatic)) }
                Text(stringResource(R.string.subscription_automatic_hint), style = MaterialTheme.typography.bodySmall)
                Button(enabled = !busy && name.isNotBlank() && url.isNotBlank(), onClick = {
                    execute { preview = repository.prepare(name, url, editing, automatic) }
                }) { Text(stringResource(R.string.subscription_preview)) }
                if (editing != null) TextButton(enabled = !busy, onClick = { editing = null; name = ""; url = ""; automatic = false }) { Text(stringResource(R.string.subscription_add)) }
                if (busy) LinearProgressIndicator(Modifier.fillMaxWidth())
                error?.let { Text(subscriptionError(it), color = MaterialTheme.colorScheme.error) }
                HorizontalDivider()
                TextButton(enabled = !busy && sources.isNotEmpty() && connection.isDisconnected, onClick = {
                    execute { sources.forEach { try { repository.refresh(it.id) } catch (e: CancellationException) { throw e } catch (_: Exception) { /* Source status is shown below. */ } } }
                }) { Text(stringResource(R.string.subscription_update_all)) }
                sources.forEach { row ->
                    Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(10.dp)) {
                        Text(row.name, style = MaterialTheme.typography.titleSmall)
                        Text(hosts[row.id] ?: "—", style = MaterialTheme.typography.bodySmall)
                        Text(stringResource(R.string.subscription_last_success, if (row.lastSuccess == 0L) "—" else java.text.DateFormat.getDateTimeInstance().format(java.util.Date(row.lastSuccess))), style = MaterialTheme.typography.bodySmall)
                        if (row.lastError.isNotEmpty()) Text(subscriptionError(row.lastError), color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
                        TextButton(enabled = !busy, onClick = {
                            editing = row.id; name = row.name; automatic = row.autoUpdate
                            url = runCatching { repository.sourceUrl(row) }.getOrDefault("")
                            execute { preview = repository.prepare(row.name, url, row.id, row.autoUpdate) }
                        }) { Text(stringResource(R.string.subscription_update_review)) }
                        TextButton(enabled = !busy, onClick = { editing = row.id; name = row.name; automatic = row.autoUpdate; url = runCatching { repository.sourceUrl(row) }.getOrDefault("") }) { Text(stringResource(R.string.subscription_edit_source)) }
                        TextButton(enabled = !busy, onClick = { execute { repository.setAutomatic(row.id, !row.autoUpdate) } }) { Text(stringResource(if (row.autoUpdate) R.string.subscription_auto_off else R.string.subscription_auto_on)) }
                        TextButton(enabled = !busy, onClick = { execute { repository.detach(row.id) } }) { Text(stringResource(R.string.subscription_detach)) }
                    } }
                }
            }
        }, confirmButton = { TextButton(onClick = { job?.cancel(); onDismiss() }) { Text(stringResource(android.R.string.cancel)) } })
    preview?.let { item ->
        AlertDialog(onDismissRequest = { if (!busy) preview = null },
            title = { Text(stringResource(R.string.subscription_preview)) },
            text = { Column {
                Text(stringResource(R.string.subscription_preview_summary, item.profiles.size, item.removed))
                LazyColumn(Modifier.heightIn(max = 220.dp)) {
                    items(item.profiles, key = { it.first }) { (_, profile) ->
                        Text(profile.name + " · " + profile.tunnelType.displayName, style = MaterialTheme.typography.bodySmall)
                    }
                }
                if (busy) LinearProgressIndicator(Modifier.fillMaxWidth())
                if (!connection.isDisconnected && onDisconnect != null) TextButton(
                    enabled = connection !is app.slipnet.domain.model.ConnectionState.Disconnecting,
                    onClick = onDisconnect) { Text(stringResource(R.string.ui_disconnect)) }
                error?.let { Text(subscriptionError(it), color = MaterialTheme.colorScheme.error) }
            } },
            confirmButton = { TextButton(enabled = !busy && connection.isDisconnected, onClick = {
                execute { repository.apply(item); preview = null; editing = null; name = ""; url = "" }
            }) { Text(stringResource(R.string.rv_apply_31e392)) } },
            dismissButton = { TextButton(onClick = { job?.cancel(); preview = null }) { Text(stringResource(android.R.string.cancel)) } })
    }
}

@Composable
private fun subscriptionError(code: String): String = stringResource(when (code) {
    "HTTPS_REQUIRED" -> R.string.subscription_https_required
    "INVALID_FEED", "UNSUPPORTED_FEED" -> R.string.subscription_invalid_feed
    "FEED_TOO_LARGE" -> R.string.subscription_too_large
    "DISCONNECT_TO_UPDATE" -> R.string.subscription_disconnect
    "REVIEW_REQUIRED" -> R.string.subscription_review_required
    "SOURCE_CHANGED" -> R.string.subscription_source_changed
    else -> R.string.subscription_failed
})
