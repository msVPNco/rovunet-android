package app.slipnet.core

import org.json.JSONArray
import org.json.JSONObject
import java.net.URI

data class UpdateAsset(val version: String, val url: String, val size: Long, val digest: String, val checksumsUrl: String, val notes: String)

object UpdateManifest {
    const val RELEASES_URL = "https://github.com/msVPNco/rovunet-app/releases"
    const val API_URL = "https://api.github.com/repos/msVPNco/rovunet-app/releases"
    private const val PREFIX = "$RELEASES_URL/download/"
    private const val DOWNLOAD_PATH = "/msVPNco/rovunet-app/releases/download/"
    private const val MAX_APK_BYTES = 400_000_000L

    fun trustedAsset(url: String): Boolean = runCatching {
        val uri = URI(url)
        url.startsWith(PREFIX) && uri.scheme == "https" && uri.host == "github.com" && uri.port == -1 &&
            uri.userInfo == null && uri.fragment == null && uri.query == null &&
            !uri.path.contains("/../") && !uri.path.contains("/./") && !uri.path.contains('\\') &&
            uri.path.startsWith(DOWNLOAD_PATH)
    }.getOrDefault(false)

    /** GitHub Latest belongs to the whole repository; desktop releases are not Android updates. */
    fun newestPublished(
        releases: JSONArray,
        previous: JSONObject? = null,
        lite: Boolean = false,
        includePreview: Boolean = true,
    ): JSONObject? {
        fun eligible(release: JSONObject): Boolean = !release.optBoolean("draft") &&
            (includePreview || !release.optBoolean("prerelease")) &&
            ReleaseVersion.supports(release.optString("tag_name")) && compatibleApk(release, lite) != null
        var best = previous?.takeIf(::eligible)
        for (i in 0 until releases.length()) {
            val release = releases.optJSONObject(i) ?: continue
            if (eligible(release) && (best == null ||
                    ReleaseVersion.isNewer(release.getString("tag_name"), best.getString("tag_name")))) {
                best = release
            }
        }
        return best
    }

    private fun assets(release: JSONObject): List<JSONObject> {
        val array = release.optJSONArray("assets") ?: return emptyList()
        return (0 until array.length()).mapNotNull(array::optJSONObject)
    }

    private fun digest(apk: JSONObject): String = apk.optString("digest").lowercase()
        .removePrefix("sha256:").takeIf { it.matches(Regex("[a-f0-9]{64}")) }.orEmpty()

    private fun assetInRelease(asset: JSONObject, release: JSONObject): Boolean {
        val url = asset.optString("browser_download_url")
        return trustedAsset(url) && URI(url).path ==
            "$DOWNLOAD_PATH${release.optString("tag_name")}/${asset.optString("name")}"
    }

    private fun checksumsUrl(release: JSONObject): String = assets(release)
        .singleOrNull { it.optString("name") == "APK-SHA256.txt" && assetInRelease(it, release) }
        ?.optString("browser_download_url").orEmpty()

    private fun compatibleApk(release: JSONObject, lite: Boolean): JSONObject? {
        val apks = assets(release).filter {
            val name = it.optString("name").lowercase()
            name.endsWith(".apk") && name.contains(if (lite) "lite" else "full") &&
                (!name.contains("lite") || lite) &&
                listOf("unsigned", "candidate", "-debug", "-review", "-private", "-test").none(name::contains)
        }
        val apk = apks.singleOrNull { it.optString("name").contains("universal", true) }
            ?: apks.singleOrNull() ?: return null
        val size = apk.optLong("size")
        val state = apk.optString("state")
        if (!assetInRelease(apk, release) || size !in 1..MAX_APK_BYTES ||
            (state.isNotEmpty() && state != "uploaded")) return null
        if (digest(apk).isEmpty() && checksumsUrl(release).isEmpty()) return null
        return apk
    }

    fun parse(body: String, current: String, lite: Boolean): UpdateAsset? {
        val json = JSONObject(body)
        val version = json.getString("tag_name").removePrefix("v")
        require(!json.optBoolean("draft") && ReleaseVersion.supports(version) && ReleaseVersion.supports(current)) { "Unrecognized release information" }
        if (!ReleaseVersion.isNewer(version, current.removeSuffix("-lite"))) return null
        val apk = compatibleApk(json, lite) ?: error("No compatible verified Android APK in release")
        return UpdateAsset(version, apk.getString("browser_download_url"), apk.getLong("size"),
            digest(apk), checksumsUrl(json), json.optString("body").take(2000))
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
