package app.slipnet.util

import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL

data class AppUpdate(
    val versionName: String,
    val downloadUrl: String,
    val releaseNotes: String
)

object UpdateChecker {
    private const val TAG = "UpdateChecker"
    const val RELEASES_URL = "https://github.com/msVPNco/rovunet-android/releases"
    private const val GITHUB_API = "https://api.github.com/repos/msVPNco/rovunet-android/releases/latest"
    private const val TIMEOUT_MS = 10_000
    // Only check once per 12 hours
    const val CHECK_INTERVAL_MS = 12 * 60 * 60 * 1000L

    /**
     * Check GitHub for a newer release. Returns null if up-to-date or on error.
     * Safe to call from any coroutine — uses Dispatchers.IO, never throws.
     */
    suspend fun check(currentVersionName: String): AppUpdate? = withContext(Dispatchers.IO) {
        if (GITHUB_API.isBlank()) return@withContext null
        try {
            val conn = URL(GITHUB_API).openConnection() as HttpURLConnection
            conn.requestMethod = "GET"
            conn.setRequestProperty("Accept", "application/vnd.github+json")
            conn.connectTimeout = TIMEOUT_MS
            conn.readTimeout = TIMEOUT_MS

            if (conn.responseCode != 200) {
                Log.d(TAG, "GitHub API returned ${conn.responseCode}")
                conn.disconnect()
                return@withContext null
            }

            val body = conn.inputStream.bufferedReader().use { it.readText() }
            conn.disconnect()

            val json = JSONObject(body)
            val tagName = json.optString("tag_name", "").removePrefix("v")
            val htmlUrl = json.optString("html_url", "")
            val releaseBody = json.optString("body", "").take(500)

            if (json.optBoolean("draft", false) || tagName.isBlank() ||
                !htmlUrl.startsWith("https://github.com/msVPNco/rovunet-android/releases/tag/")) {
                return@withContext null
            }

            if (app.slipnet.core.ReleaseVersion.isNewer(tagName, currentVersionName)) {
                Log.d(TAG, "Update available: $tagName (current: $currentVersionName)")
                AppUpdate(
                    versionName = tagName,
                    downloadUrl = htmlUrl,
                    releaseNotes = releaseBody
                )
            } else {
                Log.d(TAG, "Up to date ($currentVersionName)")
                null
            }
        } catch (e: Exception) {
            Log.d(TAG, "Update check failed: ${e.message}")
            null
        }
    }

}
