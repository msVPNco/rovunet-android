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
import kotlinx.coroutines.sync.withLock
import java.security.MessageDigest

data class AppUpdate(val versionName: String, val downloadUrl: String, val releaseNotes: String, val sha256: String, val size: Long)

/** Only HTTPS release assets from this repository; no installation without OS confirmation. */
object UpdateChecker {
    const val RELEASES_URL = UpdateManifest.RELEASES_URL
    private const val API = UpdateManifest.API_URL
    const val CHECK_INTERVAL_MS = 12 * 60 * 60 * 1000L

    private suspend fun readText(route: UpdateNetwork.Route, url: String, maxBytes: Int): String =
        route.get(url) { response ->
            check(response.code == 200) { "Update service unavailable" }
            check(response.body.contentLength() <= maxBytes) { "Update response too large" }
            response.body.byteStream().use { input ->
                val output = java.io.ByteArrayOutputStream()
                val buffer = ByteArray(8192)
                while (true) {
                    currentCoroutineContext().ensureActive()
                    route.ensureRoute()
                    val n = input.read(buffer)
                    if (n < 0) break
                    check(output.size() + n <= maxBytes) { "Update response too large" }
                    output.write(buffer, 0, n)
                }
                String(output.toByteArray(), Charsets.UTF_8)
            }
        }
    suspend fun check(context: Context, currentVersionName: String, includePreview: Boolean): AppUpdate? = withContext(Dispatchers.IO) {
        // A desktop release can be global Latest. Scan the shared feed and select
        // only this Android flavour, preserving the explicit preview preference.
        val route = UpdateNetwork.from(context).route()
        val lite = currentVersionName.endsWith("-lite")
        var newest: org.json.JSONObject? = null
        var page = 1
        while (true) {
            val releases = org.json.JSONArray(readText(route, "$API?per_page=30&page=$page", 3_000_000))
            newest = UpdateManifest.newestPublished(releases, newest, lite, includePreview)
            if (releases.length() < 30) break
            check(++page <= 10) { "Release listing incomplete" }
        }
        val body = newest?.toString() ?: return@withContext null
        val manifest = UpdateManifest.parse(body, currentVersionName, lite) ?: return@withContext null
        val digest = manifest.digest.ifEmpty { UpdateManifest.checksum(readText(route, manifest.checksumsUrl, 64_000), manifest.url) }
        AppUpdate(manifest.version, manifest.url, manifest.notes, digest, manifest.size)
    }
    private val downloadMutex = kotlinx.coroutines.sync.Mutex()
    suspend fun download(context: Context, update: AppUpdate, progress: suspend (Int) -> Unit): File =
        downloadMutex.withLock() {
            downloadLocked(context, update, progress)
        }

    private suspend fun downloadLocked(context: Context, update: AppUpdate, progress: suspend (Int) -> Unit): File = withContext(Dispatchers.IO) {
        require(UpdateManifest.trustedAsset(update.downloadUrl) && update.sha256.matches(Regex("[a-f0-9]{64}")) &&
            update.size in 1..400_000_000)
        val route = UpdateNetwork.from(context).route()
        val folder = File(context.cacheDir, "shared/updates").apply { mkdirs() }
        folder.listFiles()?.forEach { it.delete() }
        val part = File(folder, "update.part")
        val apk = File(folder, "Rovunet-update.apk")
        try {
            downloadBody(route, update, part, progress)
            verifyPackage(context, part)
            route.ensureRoute()
            check(part.renameTo(apk)) { "Could not save APK" }
            apk
        } finally { part.delete() }
    }
    internal suspend fun downloadBody(route: UpdateNetwork.Route, update: AppUpdate, part: File,
        progress: suspend (Int) -> Unit) {
            var complete = false
            var attempts = 0
            var previous = -1
            while (!complete) {
                currentCoroutineContext().ensureActive()
                route.ensureRoute()
                val offset = if (part.exists()) part.length() else 0L
                try {
                    route.get(update.downloadUrl, offset) { response ->
                        val append = response.code == 206 && offset > 0
                        check(response.code == 200 || append) { "Download unavailable" }
                        if (append) check(app.slipnet.core.UpdateNetworkPolicy.rangeStart(
                            response.header("Content-Range"), offset, update.size)) { "Unexpected APK size" }
                        check(response.header("Content-Encoding").let { it == null || it.equals("identity", true) }) { "Unexpected APK encoding" }
                        var total = if (append) offset else 0L
                        if (!append && offset > 0) { previous = -1; progress(0) }
                        java.io.FileOutputStream(part, append).use { out ->
                            response.body.byteStream().use { input ->
                                val buffer = ByteArray(64 * 1024)
                                while (true) {
                                    currentCoroutineContext().ensureActive()
                                    route.ensureRoute()
                                    val count = input.read(buffer)
                                    if (count < 0) break
                                    total += count
                                    check(total <= update.size) { "Unexpected APK size" }
                                    out.write(buffer, 0, count)
                                    val percent = (total * 100 / update.size).toInt()
                                    if (percent != previous) { previous = percent; progress(percent) }
                                }
                            }
                        }
                        if (total != update.size) throw java.io.EOFException("Incomplete APK")
                        complete = true
                    }
                } catch (error: java.io.IOException) {
                    currentCoroutineContext().ensureActive()
                    route.ensureRoute()
                    // Same-session, bounded retries only. Every resumed byte is covered by the final digest.
                    if (++attempts >= 3 || error is javax.net.ssl.SSLException) throw error
                    kotlinx.coroutines.delay(attempts * 750L)
                }
            }
            val hash = MessageDigest.getInstance("SHA-256")
            part.inputStream().use { input ->
                val buffer = ByteArray(64 * 1024)
                while (true) {
                    currentCoroutineContext().ensureActive()
                    val count = input.read(buffer); if (count < 0) break
                    hash.update(buffer, 0, count)
                }
            }
            val actual = hash.digest().joinToString("") { "%02x".format(it) }
            check(MessageDigest.isEqual(actual.toByteArray(), update.sha256.toByteArray())) { "APK checksum mismatch" }
    }
    fun errorResource(error: Throwable): Int = when {
        error.message in setOf("Update route changed", "Update route unavailable") -> app.slipnet.R.string.update_error_route
        error is java.net.UnknownHostException || error is java.net.SocketTimeoutException ||
            error is java.net.ConnectException || error.message in setOf("Update service unavailable", "Download unavailable") -> app.slipnet.R.string.update_error_network
        error.message in setOf("Wrong application package", "APK signing identity mismatch") -> app.slipnet.R.string.update_error_identity
        error.message in setOf("Incomplete APK", "APK checksum mismatch", "Invalid APK", "Unexpected APK size", "Unexpected APK encoding") -> app.slipnet.R.string.update_error_integrity
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
