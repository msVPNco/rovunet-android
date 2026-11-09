package app.slipnet.data.subscription

import app.slipnet.data.export.WireBase64
import org.json.JSONObject

/** Unknown JSON versions and partially invalid feeds are never applied. */
object SubscriptionPayload {
    data class Item(val key: String?, val link: String)
    fun read(body: String): List<Item> {
        val text = body.removePrefix("\uFEFF").trim()
        require(text.isNotBlank() && !text.startsWith("<")) { "INVALID_FEED" }
        if (text.startsWith("{")) {
            var depth = 0; var quoted = false; var escaped = false
            text.forEach { c ->
                if (quoted) {
                    if (escaped) escaped = false else if (c == '\\') escaped = true else if (c == '"') quoted = false
                } else when (c) {
                    '"' -> quoted = true
                    '{', '[' -> { depth++; require(depth <= 16) { "INVALID_FEED" } }
                    '}', ']' -> depth--
                }
            }
            val root = JSONObject(text)
            if (root.has("outbounds") || root.has("protocol") || root.has("type")) return listOf(Item(null, text))
            require(root.get("version") is Number && root.get("version").toString() == "1" && root.keys().asSequence().all { it in setOf("version", "items", "name") }) { "UNSUPPORTED_FEED" }
            val array = root.getJSONArray("items")
            require(array.length() in 1..1000) { "INVALID_FEED" }
            return (0 until array.length()).map { n ->
                val item = array.getJSONObject(n)
                require(item.keys().asSequence().all { it in setOf("id", "link") }) { "UNSUPPORTED_FEED" }
                require(item.opt("id") is String && item.opt("link") is String) { "INVALID_FEED" }
                val key = item.getString("id")
                require(key.isNotBlank() && key.length <= 200) { "INVALID_FEED" }
                Item(key, item.getString("link"))
            }.also { require(it.map(Item::key).distinct().size == it.size) { "INVALID_FEED" } }
        }
        if (text.startsWith("[") && app.slipnet.core.CoreDocument.looksLikeJson(text)) return listOf(Item(null, text))
        val expanded = if (text.contains("://") || text.startsWith("[Interface]")) text
            else runCatching { String(WireBase64.decode(text), Charsets.UTF_8) }.getOrElse { throw IllegalArgumentException("INVALID_FEED") }
        require(expanded.isNotBlank() && !expanded.trimStart().startsWith("<")) { "INVALID_FEED" }
        return listOf(Item(null, expanded))
    }
}
