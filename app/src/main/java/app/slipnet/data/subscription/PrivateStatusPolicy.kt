package app.slipnet.data.subscription

import java.net.URI

/** A persisted trust boundary, independent of provider headers and display receipts. */
internal data class PrivateStatusBinding(val sourceFingerprint: String, val serverId: String,
    val accountId: String, val highWater: Long)

internal object PrivateStatusPolicy {
    fun accepts(binding: PrivateStatusBinding?, sourceFingerprint: String, incoming: AccountStatus,
        previous: AccountStatus?, hasPriorSourceBinding: Boolean = false): Boolean {
        if (incoming.providerReported) return false
        if (binding == null) return previous == null && !hasPriorSourceBinding
        return binding.sourceFingerprint == sourceFingerprint && binding.serverId == incoming.serverId &&
            binding.accountId == incoming.accountId && incoming.revision >= binding.highWater &&
            previous != null && incoming.accepts(previous)
    }

    /** v2 status is a proposed contract only; production must not probe it yet. */
    fun endpoint(address: String, proposedV2: Boolean = false): String? = runCatching {
        val uri = URI(address)
        if (!validAuthority(uri) || uri.rawQuery != null) return null
        val match = Regex("^(/sub/|/v2/sub/)([0-9a-fA-F]{32})(\\.windows)?\\.json$").matchEntire(uri.rawPath) ?: return null
        val v2 = match.groupValues[1] == "/v2/sub/"
        if (v2 && !proposedV2) return null
        val alias = if (v2) match.groupValues[3] else ""
        URI(uri.scheme, null, uri.host, uri.port, match.groupValues[1] + match.groupValues[2] + alias + ".status.json", null, null).toASCIIString()
    }.getOrNull()

    fun redirectAllowed(initial: String, next: String, proposedV2: Boolean = false): Boolean = runCatching {
        val origin = URI(initial); val target = URI(next)
        validAuthority(origin) && validAuthority(target) && origin.host.equals(target.host, true) &&
            port(origin) == port(target) && origin.rawPath == target.rawPath &&
            origin.rawQuery == target.rawQuery &&
            Regex(if (proposedV2) "^/(?:v2/)?sub/[0-9a-fA-F]{32}(?:\\.windows)?\\.status\\.json$"
                else "^/sub/[0-9a-fA-F]{32}\\.status\\.json$").matches(origin.rawPath)
    }.getOrDefault(false)

    private fun port(uri: URI) = if (uri.port == -1) 443 else uri.port
    private fun validAuthority(uri: URI) = uri.scheme.equals("https", true) && !uri.host.isNullOrBlank() &&
        uri.rawUserInfo == null && uri.rawFragment == null && port(uri) in 1..65535
}
