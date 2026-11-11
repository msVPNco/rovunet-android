package app.slipnet.core

import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class UpdateManifestTest {
    private val hash = "a".repeat(64)
    private fun release(tag: String, names: List<String> = listOf("Rovunet-Full.apk")): JSONObject =
        JSONObject().put("tag_name", tag).put("assets", JSONArray().apply {
            names.forEach { name -> put(JSONObject().put("name", name).put("size", 100L)
                .put("state", "uploaded").put("digest", "sha256:$hash")
                .put("browser_download_url", "${UpdateManifest.RELEASES_URL}/download/$tag/$name")) }
        })
    private fun feed(vararg releases: JSONObject) = JSONArray().apply { releases.forEach(::put) }
    private fun selected(array: JSONArray, lite: Boolean = false, previews: Boolean = true): String? =
        UpdateManifest.newestPublished(array, lite = lite, includePreview = previews)?.getString("tag_name")
    private fun apk(release: JSONObject) = release.getJSONArray("assets").getJSONObject(0)

    @Test fun publicUpgradeFromAlpha15_3AndSubsequentDotVersionsUsesCanonicalAsset() {
        val r = release("v0.3.0-alpha18.1")
        for (current in listOf("0.3.0-alpha15.3", "0.3.0-alpha16.1", "0.3.0-alpha17", "0.3.0-alpha18")) {
            assertEquals("0.3.0-alpha18.1", UpdateManifest.parse(r.toString(), current, false)?.version)
        }
        assertNull(UpdateManifest.parse(r.toString(), "0.3.0-alpha18.1", false))
        assertFalse(UpdateManifest.trustedAsset(apk(r).getString("browser_download_url").replace("rovunet-app", "rovunet-android")))
    }

    @Test fun canonicalRepositoryOwnsBothApiAndDownloads() {
        assertEquals("https://api.github.com/repos/msVPNco/rovunet-app/releases", UpdateManifest.API_URL)
        val url = "${UpdateManifest.RELEASES_URL}/download/v0.3.0-alpha16.1/Rovunet-Full.apk"
        assertTrue(UpdateManifest.trustedAsset(url))
        for (bad in listOf(url.replace("rovunet-app", "rovunet-android"), url.replace("rovunet-app", "rovunet-app-fork"),
            url.replace("github.com", "github.com.evil.invalid"), url.replace("https:", "http:"),
            url + "?token=x", url + "#fragment", url.replace("github.com/", "github.com:444/"),
            url.replace("/download/", "/download/%2e%2e/"), url.replace("github.com", "user@github.com"))) {
            assertFalse(bad, UpdateManifest.trustedAsset(bad))
        }
    }

    @Test fun renamedProductionAssetIsParsedWithoutWeakeningItsDigest() {
        val r = release("v0.3.0-alpha16.1")
        val parsed = UpdateManifest.parse(r.toString(), "0.3.0-alpha16", false)!!
        assertEquals("0.3.0-alpha16.1", parsed.version)
        assertEquals(hash, parsed.digest)
        assertEquals(apk(r).getString("browser_download_url"), parsed.url)
        assertNull(UpdateManifest.parse(r.toString(), "0.3.0-alpha16.1", false))
    }

    @Test fun desktopLatestWithLargerNumericVersionCannotReplaceAndroid() {
        val android = release("v0.3.0-alpha16.1")
        val windows = release("v99.0.0", listOf("Rovunet-Full.exe", "Rovunet-Full.msi"))
        val mac = release("v100.0.0", listOf("Rovunet-Full.dmg", "Rovunet-source.zip"))
        assertEquals("v0.3.0-alpha16.1", selected(feed(mac, windows, android)))
        assertNull(selected(feed(mac, windows)))
        assertNull(selected(feed(release("windows-v99.0.0"), release("macos-v100.0.0"))))
    }

    @Test fun previewPreferenceAndDraftExclusionApplyBeforeVersionOrdering() {
        val public = release("v0.3.0-alpha16")
        val preview = release("v0.3.0-alpha16.1").put("prerelease", true)
        val draft = release("v9.0.0").put("draft", true)
        assertEquals("v0.3.0-alpha16", selected(feed(draft, preview, public), previews = false))
        assertEquals("v0.3.0-alpha16.1", selected(feed(draft, preview, public), previews = true))
    }

    @Test fun fullAndLiteHaveIndependentNewestCompatibleVersions() {
        val full = release("v0.3.0-alpha18", listOf("Rovunet-Full.apk"))
        val lite = release("v0.3.0-alpha17", listOf("Rovunet-Lite.apk"))
        assertEquals("v0.3.0-alpha18", selected(feed(lite, full)))
        assertEquals("v0.3.0-alpha17", selected(feed(full, lite), lite = true))
        assertNull(selected(feed(full), lite = true))
    }

    @Test fun universalApkWinsAndUnresolvedArchitectureAmbiguityIsRejected() {
        val r = release("v1.0.0", listOf("Rovunet-Full-arm64.apk", "Rovunet-Full-universal.apk"))
        assertTrue(UpdateManifest.parse(r.toString(), "0.3.0-alpha16", false)!!.url.endsWith("Full-universal.apk"))
        assertNull(selected(feed(release("v2.0.0", listOf("Rovunet-Full-arm64.apk", "Rovunet-Full-x86.apk")))))
    }

    @Test fun unsignedDebugPrivateAndReviewPackagesNeverEnterTheUpdateChannel() {
        for (name in listOf("Rovunet-Full-unsigned.apk", "Rovunet-Full-debug.apk", "Rovunet-Full-review01.apk",
            "Rovunet-Full-private.apk", "Rovunet-Full-candidate.apk", "Rovunet-Full-test.apk")) {
            assertNull(name, selected(feed(release("v1.0.0", listOf(name)))))
        }
        assertEquals("v1.0.0", selected(feed(release("v1.0.0", listOf("Rovunet-Full.apk", "Rovunet-Full-unsigned.apk")))))
    }

    @Test fun untrustedOrMislabelledAssetsAndInvalidSizesAreNotCandidates() {
        val r = release("v1.0.0")
        for (url in listOf(apk(r).getString("browser_download_url").replace("rovunet-app", "other"),
            "${UpdateManifest.RELEASES_URL}/download/v0.9.0/Rovunet-Full.apk",
            "${UpdateManifest.RELEASES_URL}/download/v1.0.0/other.apk")) {
            val changed = JSONObject(r.toString()); apk(changed).put("browser_download_url", url)
            assertNull(selected(feed(changed)))
        }
        for (size in listOf(-1L, 0L, 400_000_001L)) {
            val changed = JSONObject(r.toString()); apk(changed).put("size", size)
            assertNull(selected(feed(changed)))
        }
        apk(r).put("state", "new")
        assertNull(selected(feed(r)))
    }

    @Test fun trustedSameReleaseChecksumFileSupportsLegacyMissingAssetDigest() {
        val r = release("v1.0.0"); apk(r).remove("digest")
        assertNull(selected(feed(r)))
        r.getJSONArray("assets").put(JSONObject().put("name", "APK-SHA256.txt")
            .put("browser_download_url", "${UpdateManifest.RELEASES_URL}/download/v1.0.0/APK-SHA256.txt"))
        assertEquals("v1.0.0", selected(feed(r)))
        val parsed = UpdateManifest.parse(r.toString(), "0.3.0-alpha16", false)!!
        assertEquals("", parsed.digest)
        assertTrue(parsed.checksumsUrl.endsWith("/APK-SHA256.txt"))
        r.getJSONArray("assets").getJSONObject(1).put("browser_download_url", "https://example.invalid/APK-SHA256.txt")
        assertNull(selected(feed(r)))
    }

    @Test fun pagingKeepsNewestCompatibleAndroidAcrossDesktopAndOlderPages() {
        val first = UpdateManifest.newestPublished(feed(release("v2.0.0", listOf("Rovunet-Full.exe")), release("v0.3.0-alpha16")))
        val next = UpdateManifest.newestPublished(feed(release("v0.3.0-alpha16.1"), release("v0.3.0-alpha15.4")), first)
        assertEquals("v0.3.0-alpha16.1", next?.getString("tag_name"))
        assertEquals("v0.3.0-alpha16.1", UpdateManifest.newestPublished(feed(release("v3.0.0", listOf("Rovunet-Full.dmg"))), next)?.getString("tag_name"))
    }
}
