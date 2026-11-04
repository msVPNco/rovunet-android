package app.slipnet.presentation.common.components

import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.core.content.FileProvider
import app.slipnet.R
import app.slipnet.util.AppUpdate
import app.slipnet.util.UpdateChecker
import kotlinx.coroutines.launch
import java.io.File

@Composable
fun UpdateDialog(update: AppUpdate, onDismiss: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var percent by remember(update) { mutableStateOf<Int?>(null) }
    var downloaded by remember(update) { mutableStateOf<File?>(null) }
    var failed by remember(update) { mutableStateOf(false) }
    var job by remember { mutableStateOf<kotlinx.coroutines.Job?>(null) }
    val errorText = stringResource(R.string.update_failed)
    AlertDialog(
        onDismissRequest = { job?.cancel(); onDismiss() },
        title = { Text(stringResource(R.string.update_available)) },
        text = { Text(when { failed -> errorText; percent != null -> stringResource(R.string.update_downloading, percent!!); else -> stringResource(R.string.update_version, update.versionName) }) },
        confirmButton = {
            TextButton(enabled = percent == null, onClick = {
                val apk = downloaded
                if (apk != null) {
                    try {
                        if (Build.VERSION.SDK_INT >= 26 && !context.packageManager.canRequestPackageInstalls()) {
                            context.startActivity(Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${context.packageName}")))
                        } else {
                            val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", apk)
                            context.startActivity(Intent(Intent.ACTION_VIEW).setDataAndType(uri, "application/vnd.android.package-archive").addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION))
                        }
                    } catch (_: Exception) { failed = true }
                } else {
                    failed = false; percent = 0
                    job = scope.launch {
                        try {
                            downloaded = UpdateChecker.download(context.applicationContext, update) { value -> kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.Main.immediate) { percent = value } }
                        } catch (e: kotlinx.coroutines.CancellationException) { throw e }
                        catch (_: Exception) { failed = true }
                        finally { percent = null }
                    }
                }
            }) { Text(stringResource(if (downloaded != null) R.string.update_install else R.string.update_download)) }
        },
        dismissButton = { TextButton(onClick = { job?.cancel(); onDismiss() }) { Text(stringResource(android.R.string.cancel)) } }
    )
}
