package app.slipnet.presentation.settings

import android.content.Intent
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import app.slipnet.R
import app.slipnet.util.StabilityDiagnostics
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

@Composable
fun StabilityReportDialog(onDismiss: () -> Unit) {
    val resources = LocalResources.current
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var report by remember { mutableStateOf<String?>(null) }
    var shareFailed by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) { report = withContext(Dispatchers.IO) { StabilityDiagnostics.report() } }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(resources.getString(R.string.settings_stability_report_5f8404)) },
        text = { Column(Modifier.heightIn(max = 380.dp).verticalScroll(rememberScrollState())) {
            SelectionContainer { Text(report?.let { if (it == "No stability report recorded.") resources.getString(R.string.settings_no_stability_report_recorded_3b2da5) else it } ?: resources.getString(R.string.settings_loading_report_53284c)) }
            if (shareFailed) Text(resources.getString(R.string.settings_could_not_share_report_226dba))
        } },
        confirmButton = { TextButton(enabled = report != null, onClick = { scope.launch {
            try {
                val file = withContext(Dispatchers.IO) {
                    File(context.cacheDir, "shared/stability.txt").apply { parentFile?.mkdirs(); writeText(report.orEmpty()) }
                }
                val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
                context.startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).apply {
                    type = "text/plain"
                    putExtra(Intent.EXTRA_STREAM, uri)
                    addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                }, resources.getString(R.string.settings_stability_report_5f8404)))
            } catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled }
            catch (_: Exception) { shareFailed = true }
        } }) { Text(resources.getString(R.string.rv_share_29887a)) } },
        dismissButton = { TextButton(onClick = onDismiss) { Text(resources.getString(R.string.rv_close_7d9eb7)) } }
    )
}
