package app.slipnet.data.subscription

import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.export.ImportResult
import app.slipnet.data.export.WireBase64
import app.slipnet.domain.model.ProfileDuplicates
import app.slipnet.domain.model.ServerProfile
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive

/** Opt-in public feeds only. Panel/private subscriptions retain their strict all-or-nothing parser. */
object PublicFeed {
    const val MAX_BYTES = 8 * 1024 * 1024
    const val MAX_ENTRIES = 10_000
    enum class Reason { FORMAT, PROTOCOL, TRANSPORT, TLS, OPTIONS, PROTECTED }
    data class Stat(val protocol: String, val accepted: Int, val rejected: Int, val reasons: Map<Reason, Int>)
    data class Result(val profiles: List<ServerProfile>, val unsupported: Int, val duplicates: Int, val stats: List<Stat> = emptyList())
    private fun reason(line: String): Reason {
        if (line.substringBefore("://").lowercase() !in setOf("vless", "ss", "trojan", "vmess", "hysteria2", "hy2", "ssh", "socks5", "naive+https")) return Reason.PROTOCOL
        val parsed = runCatching { app.slipnet.core.CoreLink.parse(line) }
        if (parsed.getOrNull()?.outbound?.let(app.slipnet.core.CoreLink::isUnencryptedVless) == true) return Reason.TLS
        val error = parsed.exceptionOrNull()?.message.orEmpty()
        return when {
            error == "TLS_UNVERIFIED" || error.contains("Unverified TLS") || error == "TLS_REQUIRED" -> Reason.TLS
            error == "UNSUPPORTED_TRANSPORT" -> Reason.TRANSPORT
            error == "UNSUPPORTED_OPTION" || error.contains("Unsupported") -> Reason.OPTIONS
            else -> Reason.FORMAT
        }
    }
    fun expand(document: String): String {
        require(document.toByteArray(Charsets.UTF_8).size <= MAX_BYTES) { "PUBLIC_FEED_LIMIT" }
        val text = document.removePrefix("\uFEFF").trim()
        if (text.isEmpty()) return ""
        require(!text.startsWith("<") && !text.startsWith("{") && !text.startsWith("[")) { "INVALID_FEED" }
        val expanded = if (text.contains("://")) text else try {
            String(WireBase64.decode(text), Charsets.UTF_8)
        } catch (_: Exception) { error("INVALID_FEED") }
        require(expanded.toByteArray(Charsets.UTF_8).size <= MAX_BYTES) { "PUBLIC_FEED_LIMIT" }
        return expanded
    }
    suspend fun parse(document: String, importer: ConfigImporter, deviceId: String, allowEmpty: Boolean = false): Result {
        val expanded = expand(document)
        val seen = HashSet<ServerProfile>()
        val profiles = ArrayList<ServerProfile>()
        var unsupported = 0
        var duplicates = 0
        var entries = 0
        val acceptedBy = linkedMapOf<String, Int>(); val rejectedBy = linkedMapOf<String, Int>()
        val reasons = linkedMapOf<String, MutableMap<Reason, Int>>()
        expanded.lineSequence().map { it.trim() }.filter { it.isNotEmpty() && !it.startsWith("#") && !it.startsWith("//") }.forEach { line ->
            currentCoroutineContext().ensureActive()
            require(++entries <= MAX_ENTRIES) { "PUBLIC_FEED_LIMIT" }
            val protocol = when (val scheme = line.substringBefore("://").lowercase()) {
                "vless", "ss", "trojan", "vmess" -> scheme.uppercase()
                "hy2", "hysteria2" -> "Hysteria2"
                else -> "OTHER"
            }
            val parsed = if (line.length <= 65_536) importer.parseAndImport(line, deviceId) else null
            val p = (parsed as? ImportResult.Success)?.takeIf { it.warnings.isEmpty() && it.profiles.size == 1 }?.profiles?.single()
            if (p == null || p.needsCredentials || p.isExpired || p.isLocked || p.boundDeviceId.isNotEmpty()) {
                unsupported++
                rejectedBy[protocol] = (rejectedBy[protocol] ?: 0) + 1
                val why = if (p != null) Reason.PROTECTED else reason(line)
                val counts = reasons.getOrPut(protocol) { linkedMapOf() }; counts[why] = (counts[why] ?: 0) + 1
            } else if (!seen.add(ProfileDuplicates.key(p).copy(collectionId = ""))) duplicates++
            else { profiles += p; acceptedBy[protocol] = (acceptedBy[protocol] ?: 0) + 1 }
        }
        require(profiles.isNotEmpty() || (allowEmpty && entries > 0)) { "PUBLIC_FEED_EMPTY" }
        return Result(profiles, unsupported, duplicates, (acceptedBy.keys + rejectedBy.keys).distinct().map { Stat(it, acceptedBy[it] ?: 0, rejectedBy[it] ?: 0, reasons[it].orEmpty()) })
    }
}
