package app.slipnet.core

import org.json.JSONObject
import java.net.URI

data class UpdateAsset(val version: String, val url: String, val size: Long, val digest: String, val checksumsUrl: String, val notes: String)

object UpdateManifest {
    private const val PREFIX = "https://github.com/msVPNco/rovunet-android/releases/download/"
    fun trustedAsset(url: String): Boolean = runCatching {
        val uri = URI(url)
        url.startsWith(PREFIX) && uri.host == "github.com" && uri.userInfo == null && uri.fragment == null && uri.query == null &&
            !uri.path.contains("/../") && uri.path.startsWith("/msVPNco/rovunet-android/releases/download/")
    }.getOrDefault(false)
    fun parse(body: String, current: String, lite: Boolean): UpdateAsset? {
        val json = JSONObject(body)
        val version = json.getString("tag_name").removePrefix("v")
        if (json.optBoolean("draft") || !ReleaseVersion.isNewer(version, current.removeSuffix("-lite"))) return null
        val assets = json.getJSONArray("assets")
        val entries = (0 until assets.length()).map { assets.getJSONObject(it) }
        val apks = entries.filter {
            val name = it.optString("name").lowercase()
            name.endsWith(".apk") && name.contains(if (lite) "lite" else "full") && (!name.contains("lite") || lite)
        }
        val apk = apks.singleOrNull { it.optString("name").contains("universal", true) } ?: apks.singleOrNull()
            ?: error("No compatible universal APK in release")
        val url = apk.getString("browser_download_url")
        val size = apk.getLong("size")
        require(trustedAsset(url) && size in 1..400_000_000) { "Invalid update asset" }
        val digest = apk.optString("digest").removePrefix("sha256:").lowercase().takeIf { it.matches(Regex("[a-f0-9]{64}")) }.orEmpty()
        val sums = entries.singleOrNull { it.optString("name") == "APK-SHA256.txt" }?.optString("browser_download_url").orEmpty()
        require(digest.isNotEmpty() || trustedAsset(sums)) { "Update checksum unavailable" }
        return UpdateAsset(version, url, size, digest, sums, json.optString("body").take(2000))
    }
    fun checksum(text: String, assetUrl: String): String {
        val filename = URI(assetUrl).path.substringAfterLast('/')
        return text.lineSequence().map { it.trim() }.mapNotNull {
            Regex("^([a-fA-F0-9]{64})\\s+\\*?(.+)$").matchEntire(it)?.let { m ->
                m.groupValues[1].lowercase().takeIf { m.groupValues[2].substringAfterLast('/') == filename }
            }
        }.singleOrNull() ?: error("APK checksum missing or ambiguous")
    }
}
