package app.slipnet.domain.model

import com.google.gson.Gson
import org.json.JSONArray
import org.json.JSONObject
import java.security.MessageDigest

/** Local UI history only: no credentials, row-review tokens or discovered resolver overrides are serialized. */
object ConnectionHistory {
    private const val MAX_RESULTS = 2_000
    private const val MAX_AGE_MS = 7 * 24 * 60 * 60 * 1_000L
    private val gson = Gson()

    data class State(
        val selectedAnchorId: Long? = null,
        val manualIds: List<Long> = emptyList(),
        val results: List<Entry> = emptyList(),
        val sorted: Boolean = false,
        val reliability: Boolean = false
    )

    data class Entry(val id: Long, val fingerprint: String, val result: PingResult)

    data class Input(val profiles: List<ServerProfile>, val selectedCollection: String?, val choices: Map<String, Long>,
                     val results: Map<Long, PingResult>, val sorted: Boolean, val reliability: Boolean) {
        fun capture(): State = capture(profiles, selectedCollection, choices, results, sorted, reliability)
    }

    /** Only a short-lived, encrypted history entry uses this hash; never use it as a permanent profile ID. */
    private fun fingerprint(profile: ServerProfile): String = MessageDigest.getInstance("SHA-256")
        .digest(gson.toJson(ProfileDuplicates.key(profile)).toByteArray(Charsets.UTF_8))
        .let { bytes -> buildString(64) { for (byte in bytes) {
            val value = byte.toInt() and 255
            append("0123456789abcdef"[value ushr 4]); append("0123456789abcdef"[value and 15])
        } } }

    fun capture(profiles: List<ServerProfile>, selectedCollection: String?, choices: Map<String, Long>,
                results: Map<Long, PingResult>, sorted: Boolean, reliability: Boolean,
                now: Long = System.currentTimeMillis()): State {
        val byId = profiles.associateBy { it.id }
        val anchor = ConnectionChoice.rememberedPath(selectedCollection, choices, profiles)
            ?: profiles.firstOrNull { ProfileCollections.key(it) == selectedCollection }?.id
        val entries = results.mapNotNull { (id, result) ->
            val p = byId[id]?.takeUnless { it.needsCredentials } ?: return@mapNotNull null
            val measuredAt = when (result) {
                is PingResult.Success -> result.measuredAt
                is PingResult.Error -> result.measuredAt
                else -> return@mapNotNull null // Pending/Testing must never become saved results.
            }
            if (now - measuredAt !in 0..MAX_AGE_MS) null else Entry(id, fingerprint(p), result)
        }.sortedByDescending { when (val r = it.result) {
            is PingResult.Success -> r.measuredAt
            is PingResult.Error -> r.measuredAt
            else -> 0L
        } }.take(MAX_RESULTS)
        val manual = (listOfNotNull(choices[selectedCollection]) + choices.values).distinct().filter { it in byId }.take(MAX_RESULTS)
        return State(anchor, manual, entries, sorted, reliability)
    }

    fun choices(state: State, profiles: List<ServerProfile>): Map<String, Long> {
        val byId = profiles.associateBy { it.id }
        return state.manualIds.mapNotNull { byId[it] }.associate { ProfileCollections.key(it) to it.id }
    }

    fun restoreResults(state: State, profiles: List<ServerProfile>, now: Long = System.currentTimeMillis()): Map<Long, PingResult> {
        val byId = profiles.associateBy { it.id }
        val restored = state.results.mapNotNull { entry ->
            val profile = byId[entry.id]?.takeUnless { it.needsCredentials } ?: return@mapNotNull null
            val measuredAt = when (val result = entry.result) {
                is PingResult.Success -> result.measuredAt
                is PingResult.Error -> result.measuredAt
                else -> return@mapNotNull null
            }
            if (now - measuredAt !in 0..MAX_AGE_MS || fingerprint(profile) != entry.fingerprint) null
            else entry.id to entry.result
        }.toMap()
        // A restart does not prove that the underlying network is unchanged.
        return ProbeCache.historical(restored)
    }

    fun encode(state: State): String = JSONObject().apply {
        put("v", 1)
        state.selectedAnchorId?.let { put("selected", it) }
        put("choices", JSONArray(state.manualIds.take(MAX_RESULTS)))
        put("sorted", state.sorted)
        put("reliability", state.reliability)
        put("results", JSONArray().apply {
            state.results.take(MAX_RESULTS).forEach { entry ->
                val result = entry.result
                if (result !is PingResult.Success && result !is PingResult.Error) return@forEach
                put(JSONObject().apply {
                    put("id", entry.id); put("fingerprint", entry.fingerprint)
                    when (result) {
                        is PingResult.Success -> {
                            put("kind", "success"); put("at", result.measuredAt)
                            put("latency", result.latencyMs); put("verified", result.verified)
                            put("successes", result.successes); put("attempts", result.attempts)
                            put("jitter", result.jitterMs); put("setup", result.setupMs); put("quick", result.quick)
                            put("handshake", result.handshake)
                        }
                        is PingResult.Error -> {
                            put("kind", "error"); put("at", result.measuredAt)
                            put("message", result.message.take(256))
                        }
                        else -> Unit
                    }
                })
            }
        })
    }.toString()

    fun decode(text: String): State = runCatching {
        require(text.length <= 2_000_000)
        val root = JSONObject(text)
        require(root.getInt("v") == 1)
        val choices = root.optJSONArray("choices") ?: JSONArray()
        val entries = root.optJSONArray("results") ?: JSONArray()
        require(choices.length() <= MAX_RESULTS && entries.length() <= MAX_RESULTS)
        State(root.optLong("selected", 0).takeIf { it > 0 },
            (0 until choices.length()).map { choices.getLong(it) }.filter { it > 0 }.distinct(),
            (0 until entries.length()).mapNotNull { index -> runCatching {
                val item = entries.getJSONObject(index)
                val id = item.getLong("id")
                val hash = item.getString("fingerprint")
                val at = item.getLong("at")
                require(id > 0 && at > 0 && Regex("[a-f0-9]{64}").matches(hash))
                val result = when (item.getString("kind")) {
                    "success" -> {
                        val latency = item.getLong("latency")
                        val successes = item.getInt("successes")
                        val attempts = item.getInt("attempts")
                        val jitter = item.getLong("jitter")
                        val setup = item.getLong("setup")
                        require(latency >= 0 && jitter >= 0 && setup >= 0 && attempts in 1..3 && successes in 1..attempts)
                        PingResult.Success(latency, item.getBoolean("verified"), successes, attempts, jitter, setup, at,
                            item.optBoolean("quick"), currentNetwork = false, handshake = item.optBoolean("handshake"))
                    }
                    "error" -> PingResult.Error(item.getString("message").take(256), at, currentNetwork = false)
                    else -> error("Unknown measurement kind")
                }
                Entry(id, hash, result)
            }.getOrNull() }, root.optBoolean("sorted"), root.optBoolean("reliability"))
    }.getOrDefault(State())
}
