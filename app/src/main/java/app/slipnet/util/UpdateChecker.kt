package app.slipnet.util

import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import app.slipnet.core.UpdateManifest
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.withContext
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest

data class AppUpdate(val versionName: String, val downloadUrl: String, val releaseNotes: String, val sha256: String, val size: Long)

/** Only HTTPS release assets from this repository; no installation without OS confirmation. */
object UpdateChecker {
    const val RELEASES_URL = UpdateManifest.RELEASES_URL
    private const val API = UpdateManifest.API_URL
    const val CHECK_INTERVAL_MS = 12 * 60 * 60 * 1000L

    private fun connection(url: String) = (URL(url).openConnection() as HttpURLConnection).apply {
        require(URL(url).protocol == "https")
        connectTimeout = 15_000
        readTimeout = 15_000
        useCaches = false
        setRequestProperty("Cache-Control", "no-cache")
        setRequestProperty("Accept", "application/vnd.github+json")
        setRequestProperty("User-Agent", "Rovunet-Android")
    }
    private fun readText(url: String, maxBytes: Int): String {
        val conn = connection(url)
        try {
            check(conn.responseCode == 200) { "Update service unavailable" }
            return conn.inputStream.use { input ->
                val output = java.io.ByteArrayOutputStream()
                val buffer = ByteArray(8192)
                while (output.size() <= maxBytes) {
                    val n = input.read(buffer, 0, minOf(buffer.size, maxBytes + 1 - output.size()))
                    if (n < 0) break
                    output.write(buffer, 0, n)
                }
                val bytes = output.toByteArray()
                check(bytes.size <= maxBytes) { "Update response too large" }
                String(bytes, Charsets.UTF_8)
            }
        } finally { conn.disconnect() }
    }
    suspend fun check(currentVersionName: String, includePreview: Boolean): AppUpdate? = withContext(Dispatchers.IO) {
        // A desktop release can be global Latest. Scan the shared feed and select
        // only this Android flavour, preserving the explicit preview preference.
        val lite = currentVersionName.endsWith("-lite")
        var newest: org.json.JSONObject? = null
        var page = 1
        while (true) {
            val releases = org.json.JSONArray(readText("$API?per_page=30&page=$page", 3_000_000))
            newest = UpdateManifest.newestPublished(releases, newest, lite, includePreview)
            if (releases.length() < 30) break
            check(++page <= 10) { "Release listing incomplete" }
        }
        val body = newest?.toString() ?: return@withContext null
        val manifest = UpdateManifest.parse(body, currentVersionName, lite) ?: return@withContext null
        val digest = manifest.digest.ifEmpty { UpdateManifest.checksum(readText(manifest.checksumsUrl, 64_000), manifest.url) }
        AppUpdate(manifest.version, manifest.url, manifest.notes, digest, manifest.size)
    }
    suspend fun download(context: Context, update: AppUpdate, progress: suspend (Int) -> Unit): File = withContext(Dispatchers.IO) {
        require(UpdateManifest.trustedAsset(update.downloadUrl) && update.sha256.matches(Regex("[a-f0-9]{64}")))
        val folder = File(context.cacheDir, "shared/updates").apply { mkdirs() }
        folder.listFiles()?.forEach { it.delete() }
        val part = File(folder, "update.part")
        val apk = File(folder, "Rovunet-update.apk")
        val conn = connection(update.downloadUrl)
        try {
            check(conn.responseCode == 200) { "Download unavailable" }
            val hash = MessageDigest.getInstance("SHA-256")
            var total = 0L
            var previous = -1
            conn.inputStream.use { input -> part.outputStream().use { out ->
                val buffer = ByteArray(64 * 1024)
                while (true) {
                    currentCoroutineContext().ensureActive()
                    val count = input.read(buffer)
                    if (count < 0) break
                    total += count
                    check(total <= update.size && total <= 400_000_000) { "Unexpected APK size" }
                    out.write(buffer, 0, count); hash.update(buffer, 0, count)
                    val percent = (total * 100 / update.size).toInt()
                    if (percent != previous) { previous = percent; progress(percent) }
                }
            } }
            check(total == update.size) { "Incomplete APK" }
            val actual = hash.digest().joinToString("") { "%02x".format(it) }
            check(MessageDigest.isEqual(actual.toByteArray(), update.sha256.toByteArray())) { "APK checksum mismatch" }
            verifyPackage(context, part)
            check(part.renameTo(apk)) { "Could not save APK" }
            apk
        } finally { conn.disconnect(); part.delete() }
    }
    fun errorResource(error: Throwable): Int = when {
        error is java.net.UnknownHostException || error is java.net.SocketTimeoutException ||
            error is java.net.ConnectException || error.message in setOf("Update service unavailable", "Download unavailable") -> app.slipnet.R.string.update_error_network
        error.message in setOf("Wrong application package", "APK signing identity mismatch") -> app.slipnet.R.string.update_error_identity
        error.message in setOf("Incomplete APK", "APK checksum mismatch", "Invalid APK", "Unexpected APK size") -> app.slipnet.R.string.update_error_integrity
        error.message == "Could not save APK" || error is java.io.FileNotFoundException -> app.slipnet.R.string.update_error_storage
        else -> app.slipnet.R.string.update_failed
    }
    @Suppress("DEPRECATION")
    private fun verifyPackage(context: Context, file: File) {
        val pm = context.packageManager
        val flags = if (Build.VERSION.SDK_INT >= 28) PackageManager.GET_SIGNING_CERTIFICATES else PackageManager.GET_SIGNATURES
        val archive = pm.getPackageArchiveInfo(file.absolutePath, flags) ?: error("Invalid APK")
        val installed = pm.getPackageInfo(context.packageName, flags)
        check(archive.packageName == context.packageName) { "Wrong application package" }
        val oldVersion = if (Build.VERSION.SDK_INT >= 28) installed.longVersionCode else installed.versionCode.toLong()
        val newVersion = if (Build.VERSION.SDK_INT >= 28) archive.longVersionCode else archive.versionCode.toLong()
        check(newVersion > oldVersion) { "APK is not newer" }
        fun signers(info: android.content.pm.PackageInfo): Set<String> {
            val signatures = if (Build.VERSION.SDK_INT >= 28) info.signingInfo?.apkContentsSigners else info.signatures
            return signatures.orEmpty().map { signature -> MessageDigest.getInstance("SHA-256").digest(signature.toByteArray()).joinToString("") { "%02x".format(it) } }.toSet()
        }
        val expected = signers(installed)
        check(expected.isNotEmpty() && signers(archive) == expected) { "APK signing identity mismatch" }
    }
}
