package app.slipnet.presentation.common.components

import android.graphics.Bitmap
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import app.slipnet.R
import com.google.zxing.BarcodeFormat
import com.google.zxing.EncodeHintType
import com.google.zxing.qrcode.QRCodeWriter
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

fun generateQrCodeBitmap(content: String, size: Int = 768): Bitmap? {
    if (content.toByteArray(Charsets.UTF_8).size > 2400) return null
    return runCatching {
        val matrix = QRCodeWriter().encode(content, BarcodeFormat.QR_CODE, size, size,
            mapOf(EncodeHintType.CHARACTER_SET to "UTF-8", EncodeHintType.MARGIN to 4))
        Bitmap.createBitmap(size, size, Bitmap.Config.RGB_565).also { bitmap ->
            val pixels = IntArray(size * size) { n -> if (matrix[n % size, n / size]) android.graphics.Color.BLACK else android.graphics.Color.WHITE }
            bitmap.setPixels(pixels, 0, size, 0, 0, size, size)
        }
    }.getOrNull()
}

@Composable
fun QrCodeDialog(profileName: String, configUri: String, onDismiss: () -> Unit) {
    var rendered by remember(configUri) { mutableStateOf(false) }
    val bitmap by produceState<Bitmap?>(null, configUri) {
        value = withContext(Dispatchers.Default) { generateQrCodeBitmap(configUri) }
        rendered = true
    }
    val context = LocalContext.current
    val resources = LocalResources.current
    val scope = rememberCoroutineScope()
    var failed by remember { mutableStateOf(false) }
    val save = rememberLauncherForActivityResult(ActivityResultContracts.CreateDocument("image/png")) { uri ->
        if (uri != null) scope.launch {
            failed = runCatching { withContext(Dispatchers.IO) {
                context.contentResolver.openOutputStream(uri)?.use { out -> check(bitmap?.compress(Bitmap.CompressFormat.PNG, 100, out) == true) }
                    ?: error("No output")
            } }.isFailure
        }
    }
    AlertDialog(onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.ui_qr_title)) },
        text = { Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState()), horizontalAlignment = Alignment.CenterHorizontally) {
            Text(profileName, style = MaterialTheme.typography.titleMedium)
            Spacer(Modifier.height(12.dp))
            bitmap?.let { Image(it.asImageBitmap(), stringResource(R.string.ui_qr_description, profileName), Modifier.size(256.dp)) }
                ?: if (!rendered) CircularProgressIndicator() else Text(stringResource(if (configUri.toByteArray().size > 2400) R.string.qr_large else R.string.ui_qr_failed))
            Text(stringResource(R.string.ui_qr_hint), style = MaterialTheme.typography.bodySmall)
            Text(stringResource(R.string.qr_privacy_hint), style = MaterialTheme.typography.bodySmall)
            if (failed) Text(stringResource(R.string.ui_qr_failed), color = MaterialTheme.colorScheme.error)
            TextButton(enabled = bitmap != null, onClick = { save.launch("Rovunet-QR.png") }) { Text(stringResource(R.string.qr_save_image)) }
            TextButton(enabled = bitmap != null, onClick = {
                scope.launch {
                    try {
                        val uri = withContext(Dispatchers.IO) {
                            val dir = java.io.File(context.cacheDir, "shared").apply { mkdirs() }
                            dir.listFiles()?.filter { it.name.startsWith("qr-") && it.lastModified() < System.currentTimeMillis() - 86400000 }?.forEach { it.delete() }
                            val file = java.io.File(dir, "qr-${java.util.UUID.randomUUID()}.png")
                            file.outputStream().use { check(bitmap?.compress(Bitmap.CompressFormat.PNG, 100, it) == true) }
                            FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
                        }
                        context.startActivity(android.content.Intent.createChooser(android.content.Intent(android.content.Intent.ACTION_SEND).apply {
                            type = "image/png"; putExtra(android.content.Intent.EXTRA_STREAM, uri)
                            clipData = android.content.ClipData.newRawUri("QR", uri)
                            addFlags(android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION)
                        }, resources.getString(R.string.qr_share_image)))
                    } catch (_: Exception) { failed = true }
                }
            }) { Text(stringResource(R.string.qr_share_image)) }
        } }, confirmButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.rv_done_11a676)) } })
}
