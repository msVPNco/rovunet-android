package app.slipnet.core

import java.net.URI

/** Only GitHub release infrastructure; a redirect cannot broaden the updater's trust boundary. */
object UpdateNetworkPolicy {
    fun allows(address: String, redirect: Boolean = false): Boolean = runCatching {
        val uri = URI(address)
        if (uri.scheme != "https" || uri.userInfo != null || uri.fragment != null ||
            uri.port !in listOf(-1, 443) || uri.rawPath.contains('\\')) return false
        when (uri.host) {
            "api.github.com" -> uri.path == "/repos/msVPNco/rovunet-app/releases"
            "github.com" -> UpdateManifest.trustedAsset(address)
            "release-assets.githubusercontent.com", "objects.githubusercontent.com",
            "github-releases.githubusercontent.com" -> redirect
            else -> false
        }
    }.getOrDefault(false)

    fun rangeStart(header: String?, offset: Long, size: Long): Boolean {
        val match = Regex("bytes ([0-9]+)-([0-9]+)/([0-9]+)").matchEntire(header.orEmpty()) ?: return false
        val (start, end, total) = match.destructured
        return start.toLongOrNull() == offset && total.toLongOrNull() == size &&
            end.toLongOrNull() == size - 1 && offset in 0 until size
    }
}
