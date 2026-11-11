package app.slipnet.presentation.home

import android.content.Intent
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import app.slipnet.BuildConfig
import app.slipnet.R
import app.slipnet.util.AppLog
import app.slipnet.util.ConnectionDiagnostics
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** Safe report is the default and the only content exported by these controls. */
@Composable
fun DebugLogSheet(onDismiss: () -> Unit) {
    val context = LocalContext.current
    val messageResources = androidx.compose.ui.platform.LocalResources.current
    val clipboard = LocalClipboardManager.current
    val scope = rememberCoroutineScope()
    val snackbar = remember { SnackbarHostState() }
    var report by remember { mutableStateOf("") }
    var rawVisible by remember { mutableStateOf(false) }
    val raw by AppLog.lines.collectAsState()
    fun snapshot() = "Rovunet ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})\n" + ConnectionDiagnostics.report()
    LaunchedEffect(Unit) {
        while (true) {
            report = withContext(Dispatchers.Default) { snapshot() }
            AppLog.flushIfDirty()
            delay(750)
        }
    }
    DisposableEffect(rawVisible) {
        val observing = rawVisible
        if (observing) AppLog.addObserver()
        onDispose { if (observing) AppLog.removeObserver() }
    }
    val saveReport = rememberLauncherForActivityResult(ActivityResultContracts.CreateDocument("text/plain")) { uri ->
        if (uri != null) {
            val text = snapshot()
            scope.launch {
                val saved = withContext(Dispatchers.IO) {
                    runCatching {
                        val stream = context.contentResolver.openOutputStream(uri) ?: error("No output stream")
                        stream.use { it.write(text.toByteArray(Charsets.UTF_8)) }
                    }.isSuccess
                }
                snackbar.showSnackbar(messageResources.getString(if (saved) R.string.diag_saved else R.string.diag_save_failed))
            }
        }
    }
    Dialog(onDismissRequest = onDismiss, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
            Column(Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing)) {
                Row(Modifier.fillMaxWidth().padding(start = 16.dp, end = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                    Text(stringResource(R.string.diag_title), Modifier.weight(1f), style = MaterialTheme.typography.titleLarge)
                    IconButton(onClick = onDismiss) { Icon(Icons.Default.Close, stringResource(R.string.rv_close_7d9eb7)) }
                }
                Text(stringResource(R.string.diag_safe_notice), Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
                    style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Row(Modifier.fillMaxWidth().padding(horizontal = 8.dp), horizontalArrangement = Arrangement.SpaceEvenly) {
                    IconButton(onClick = { clipboard.setText(AnnotatedString(snapshot())) }) { Icon(Icons.Default.ContentCopy, stringResource(R.string.diag_copy)) }
                    IconButton(onClick = {
                        val intent = Intent(Intent.ACTION_SEND).apply { type = "text/plain"; putExtra(Intent.EXTRA_TEXT, snapshot()) }
                        context.startActivity(Intent.createChooser(intent, messageResources.getString(R.string.diag_share)))
                    }) { Icon(Icons.Default.Share, stringResource(R.string.diag_share)) }
                    IconButton(onClick = { saveReport.launch("Rovunet-diagnostics.txt") }) { Icon(Icons.Default.SaveAlt, stringResource(R.string.diag_save)) }
                    IconButton(onClick = { ConnectionDiagnostics.clear(); AppLog.clear(); report = snapshot() }) { Icon(Icons.Default.DeleteSweep, stringResource(R.string.diag_clear)) }
                }
                TextButton(onClick = { rawVisible = !rawVisible }, modifier = Modifier.padding(horizontal = 8.dp)) {
                    Text(stringResource(if (rawVisible) R.string.diag_show_safe else R.string.diag_show_raw))
                }
                if (rawVisible) Text(stringResource(R.string.diag_raw_warning), Modifier.padding(horizontal = 16.dp),
                    color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
                LazyColumn(Modifier.weight(1f).fillMaxWidth().padding(12.dp)) {
                    if (rawVisible) items(raw, key = { it.id }) { entry ->
                        Text(entry.raw, Modifier.padding(vertical = 2.dp), fontFamily = FontFamily.Monospace,
                            style = MaterialTheme.typography.bodySmall,
                            color = if (entry.level == 'E') MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurface)
                    } else item {
                        androidx.compose.runtime.CompositionLocalProvider(androidx.compose.ui.platform.LocalLayoutDirection provides androidx.compose.ui.unit.LayoutDirection.Ltr) {
                            Text(report, fontFamily = FontFamily.Monospace, style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurface)
                        }
                    }
                }
                SnackbarHost(snackbar)
            }
        }
    }
}
