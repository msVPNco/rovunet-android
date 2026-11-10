package app.slipnet.data.subscription

import app.slipnet.data.export.WireBase64
import org.json.JSONObject

data class DownloadMetadata(val userInfo: String?, val serverTime: Long, val receivedAt: Long)

/** Provider-declared snapshot, not the panel's independently sampled/revisioned status. */
internal object SubscriptionMetadata {
    fun name(entered: String, title: String?, host: String, existing: String? = null): String {
        if (entered.isNotBlank()) return entered.trim().take(120)
        if (!existing.isNullOrBlank()) return existing
        val decoded = runCatching {
            if (title?.startsWith("base64:") == true) String(WireBase64.decode(title.substring(7)), Charsets.UTF_8) else title
        }.getOrNull()?.trim()
        return decoded?.takeIf { it.isNotBlank() && it.length <= 120 && it.none(Char::isISOControl) && "://" !in it && '/' !in it }
            ?: host
    }

    fun usage(header: String, received: Long): JSONObject {
        require(header.length <= 2048)
        val fields = linkedMapOf<String, Long>()
        header.split(';').filter { it.isNotBlank() }.forEach {
            val pair = it.trim().split('=', limit = 2)
            require(pair.size == 2 && pair[0] !in fields)
            require(pair[1].matches(Regex("[0-9]+")))
            fields[pair[0]] = pair[1].toLong()
        }
        require(fields.keys.all { it in setOf("upload", "download", "total", "expire") })
        // Missing volume fields are unknown; never turn them into zero/unlimited.
        val used = if (fields.containsKey("upload") && fields.containsKey("download"))
            Math.addExact(fields.getValue("upload"), fields.getValue("download")) else null
        val expiry = fields["expire"]?.takeIf { it > 0 }?.let { Math.multiplyExact(it, 1000L) }
        val limit = fields["total"]?.takeIf { it > 0 }
        require(fields.isNotEmpty() && (limit == null || used != null))
        return JSONObject().put("source", "subscription-userinfo").put("used", used ?: JSONObject.NULL)
            .put("limit", limit ?: JSONObject.NULL).put("volume_known", fields.containsKey("total") && used != null)
            .put("expiry", expiry ?: JSONObject.NULL).put("expiry_known", fields.containsKey("expire"))
            .put("received", received)
    }

    fun readStatus(payload: String): AccountStatus {
        val root = JSONObject(payload)
        if (root.optString("source") != "subscription-userinfo") return AccountStatus.read(payload)
        require(root.has("volume_known") && root.has("expiry_known"))
        val used = if (root.isNull("used")) null else root.getLong("used")
        val limit = if (root.isNull("limit")) null else root.getLong("limit")
        val expiry = if (root.isNull("expiry")) null else root.getLong("expiry")
        val at = root.getLong("received")
        return AccountStatus("", "", "", "active", expiry, limit, used, "provider_reported", at, at, 1, true, root.getBoolean("volume_known"), root.getBoolean("expiry_known"))
    }
}
