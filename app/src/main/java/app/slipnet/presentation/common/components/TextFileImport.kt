package app.slipnet.presentation.common.components

import android.net.Uri
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.Composable
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import app.slipnet.R
import app.slipnet.core.BoundedTextInput
import kotlinx.coroutines.*

/** File providers and decoding run off the UI thread; failed reads never call the apply callback. */
@Composable
fun rememberTextFileImport(limit: Int = 2 * 1024 * 1024, onText: (String) -> Unit) = run {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val callback = rememberUpdatedState(onText)
    val message = stringResource(R.string.file_import_invalid)
    rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri: Uri? ->
        if (uri != null) scope.launch {
            try {
                val text = withContext(Dispatchers.IO) {
                    val job = currentCoroutineContext()
                    val stream = context.contentResolver.openInputStream(uri) ?: error("File unavailable")
                    coroutineScope {
                        val cancellation = launch(Dispatchers.IO, start = CoroutineStart.UNDISPATCHED) {
                            try { awaitCancellation() } finally { stream.close() }
                        }
                        try { stream.use { BoundedTextInput.read(it, limit) { job.ensureActive() } } }
                        finally { cancellation.cancel() }
                    }
                }
                callback.value(text)
            } catch (cancelled: CancellationException) { throw cancelled }
            catch (_: Exception) { Toast.makeText(context, message, Toast.LENGTH_LONG).show() }
        }
    }
}
