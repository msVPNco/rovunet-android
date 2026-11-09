package app.slipnet.data.subscription

import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.export.ImportResult
import app.slipnet.data.export.WireBase64
import app.slipnet.domain.model.ProfileDuplicates
import app.slipnet.domain.model.ServerProfile
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive

/** Opt-in public feeds only. Panel/private subscriptions retain their strict all-or-nothing parser. */
internal object PublicFeed {
    const val MAX_BYTES = 8 * 1024 * 1024
    const val MAX_ENTRIES = 10_000
    data class Result(val profiles: List<ServerProfile>, val unsupported: Int, val duplicates: Int)
    fun expand(document: String): String {
        require(document.toByteArray(Charsets.UTF_8).size <= MAX_BYTES) { "PUBLIC_FEED_LIMIT" }
        val text = document.removePrefix("\uFEFF").trim()
        require(!text.startsWith("<") && !text.startsWith("{") && !text.startsWith("[")) { "INVALID_FEED" }
        val expanded = if (text.contains("://")) text else try {
            String(WireBase64.decode(text), Charsets.UTF_8)
        } catch (_: Exception) { error("INVALID_FEED") }
        require(expanded.toByteArray(Charsets.UTF_8).size <= MAX_BYTES) { "PUBLIC_FEED_LIMIT" }
        return expanded
    }
    suspend fun parse(document: String, importer: ConfigImporter, deviceId: String): Result {
        val expanded = expand(document)
        val seen = HashSet<ServerProfile>()
        val profiles = ArrayList<ServerProfile>()
        var unsupported = 0
        var duplicates = 0
        var entries = 0
        expanded.lineSequence().map { it.trim() }.filter { it.isNotEmpty() && !it.startsWith("#") && !it.startsWith("//") }.forEach { line ->
            currentCoroutineContext().ensureActive()
            require(++entries <= MAX_ENTRIES) { "PUBLIC_FEED_LIMIT" }
            val parsed = if (line.length <= 65_536) importer.parseAndImport(line, deviceId) else null
            val p = (parsed as? ImportResult.Success)?.takeIf { it.warnings.isEmpty() && it.profiles.size == 1 }?.profiles?.single()
            if (p == null || p.needsCredentials || p.isExpired || p.isLocked || p.boundDeviceId.isNotEmpty()) unsupported++
            else if (!seen.add(ProfileDuplicates.key(p).copy(collectionId = ""))) duplicates++
            else profiles += p
        }
        require(profiles.isNotEmpty()) { "PUBLIC_FEED_EMPTY" }
        return Result(profiles, unsupported, duplicates)
    }
}
