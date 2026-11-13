package app.slipnet.data.subscription

import app.slipnet.data.export.WireBase64
import org.json.JSONObject

data class DownloadMetadata(val userInfo: String?, val serverTime: Long, val receivedAt: Long)

internal enum class ProviderLimit { FINITE, UNLIMITED, UNKNOWN }
internal enum class ProviderUsage { COMPLETE, PARTIAL, UNKNOWN }
internal enum class ProviderExpiry { AT, NONE, UNKNOWN }

/** Public advisory DTO, deliberately independent of private status v1. */
internal data class ProviderMetadata(val limit: ProviderLimit, val limitBytes: Long?,
    val usage: ProviderUsage, val usedBytes: Long?, val lowerBound: Long?,
    val expiry: ProviderExpiry, val expiresAt: Long?, val receivedAt: Long) {
    init {
        require(if (limit == ProviderLimit.FINITE) limitBytes != null && limitBytes > 0 else limitBytes == null)
        require(if (usage == ProviderUsage.COMPLETE) usedBytes != null && usedBytes >= 0 && lowerBound == null else usedBytes == null)
        require(if (usage == ProviderUsage.PARTIAL) lowerBound != null && lowerBound >= 0 else lowerBound == null)
        require(if (expiry == ProviderExpiry.AT) expiresAt != null && expiresAt > 0 else expiresAt == null)
    }
    fun json() = JSONObject().put("source", "subscription-userinfo").put("metadata_version", 2)
        .put("limit_kind", limit.name).put("usage_kind", usage.name).put("expiry_kind", expiry.name)
        .put("used", usedBytes ?: JSONObject.NULL).put("lower_bound", lowerBound ?: JSONObject.NULL)
        .put("limit", limitBytes ?: JSONObject.NULL).put("volume_known", limit != ProviderLimit.UNKNOWN)
        .put("expiry", expiresAt ?: JSONObject.NULL).put("expiry_known", expiry != ProviderExpiry.UNKNOWN)
        .put("received", receivedAt)

    fun display() = AccountStatus("", "", "", "active", expiresAt, limitBytes, usedBytes,
        "provider_reported", receivedAt, receivedAt, 1, true, limit != ProviderLimit.UNKNOWN,
        expiry != ProviderExpiry.UNKNOWN, lowerBound)
}

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

    fun usage(header: String, received: Long): JSONObject = parse(header, received).json()

    fun parse(header: String, received: Long): ProviderMetadata {
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
        require(fields.isNotEmpty())
        return ProviderMetadata(when { "total" !in fields -> ProviderLimit.UNKNOWN; limit == null -> ProviderLimit.UNLIMITED; else -> ProviderLimit.FINITE }, limit,
            when { used != null -> ProviderUsage.COMPLETE; "upload" in fields || "download" in fields -> ProviderUsage.PARTIAL; else -> ProviderUsage.UNKNOWN }, used,
            if (used == null) fields["upload"] ?: fields["download"] else null,
            when { "expire" !in fields -> ProviderExpiry.UNKNOWN; expiry == null -> ProviderExpiry.NONE; else -> ProviderExpiry.AT }, expiry, received)
    }

    fun readStatus(payload: String): AccountStatus {
        val root = JSONObject(payload)
        if (root.optString("source") != "subscription-userinfo") return AccountStatus.read(payload)
        require(root.has("volume_known") && root.has("expiry_known"))
        val used = if (root.isNull("used")) null else root.getLong("used")
        val limit = if (root.isNull("limit")) null else root.getLong("limit")
        val expiry = if (root.isNull("expiry")) null else root.getLong("expiry")
        val at = root.getLong("received")
        require(!root.has("metadata_version") || root.get("metadata_version").toString() == "2")
        require(used == null || used >= 0); require(limit == null || limit > 0)
        // Legacy provider-cache rows remain readable. They never become private records.
        val limitKind = if (root.has("metadata_version")) ProviderLimit.valueOf(root.getString("limit_kind"))
            else if (!root.getBoolean("volume_known")) ProviderLimit.UNKNOWN else if (limit == null) ProviderLimit.UNLIMITED else ProviderLimit.FINITE
        val usageKind = if (root.has("metadata_version")) ProviderUsage.valueOf(root.getString("usage_kind")) else if (used == null) ProviderUsage.UNKNOWN else ProviderUsage.COMPLETE
        val expiryKind = if (root.has("metadata_version")) ProviderExpiry.valueOf(root.getString("expiry_kind"))
            else if (!root.getBoolean("expiry_known")) ProviderExpiry.UNKNOWN else if (expiry == null) ProviderExpiry.NONE else ProviderExpiry.AT
        val lower = if (!root.has("lower_bound") || root.isNull("lower_bound")) null else root.getLong("lower_bound").also { require(it >= 0) }
        return ProviderMetadata(limitKind, limit, usageKind, used, lower, expiryKind, expiry, at).display()
    }
}
