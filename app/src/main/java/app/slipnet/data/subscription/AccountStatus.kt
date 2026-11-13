package app.slipnet.data.subscription

import java.time.Instant
import kotlin.math.abs
import org.json.JSONObject

/** Advisory panel metadata: never grants, blocks or changes a saved connection. */
data class AccountStatus(val serverId: String, val accountId: String, val periodId: String,
    val state: String, val expiresAt: Long?, val limitBytes: Long?, val usedBytes: Long?,
    val metering: String, val sampledAt: Long?, val generatedAt: Long, val revision: Long, val providerReported: Boolean = false, val volumeKnown: Boolean = true, val expiryKnown: Boolean = true,
    val usageLowerBound: Long? = null) {
    val remainingBytes: Long? get() = if (metering in setOf("partial", "unavailable")) null else
        limitBytes?.let { limit -> usedBytes?.let { (limit - it).coerceAtLeast(0) } }
    fun sameAccountPeriod(other: AccountStatus) = serverId == other.serverId && accountId == other.accountId && periodId == other.periodId
    fun accepts(previous: AccountStatus): Boolean {
        if (providerReported || previous.providerReported || serverId != previous.serverId || accountId != previous.accountId) return false
        // Period renewal keeps the bound account/server and advances the source's
        // independent high-water revision. Equal-revision period changes fail below.
        if (revision < previous.revision) return false
        return revision != previous.revision || copy(sampledAt = null, generatedAt = 0) == previous.copy(sampledAt = null, generatedAt = 0)
    }
    fun json(): String = JSONObject().put("version", 1).put("server_id", serverId).put("account_id", accountId)
        .put("period_id", periodId).put("account_status", state)
        .put("expires_at", expiresAt?.let { Instant.ofEpochMilli(it).toString() } ?: JSONObject.NULL)
        .put("limit_bytes", limitBytes ?: JSONObject.NULL).put("used_bytes", usedBytes ?: JSONObject.NULL)
        .put("metering_status", metering).put("sampled_at", sampledAt?.let { Instant.ofEpochMilli(it).toString() } ?: JSONObject.NULL)
        .put("generated_at", Instant.ofEpochMilli(generatedAt).toString()).put("revision", revision).toString()
    companion object {
        fun read(text: String): AccountStatus {
            require(text.length <= 65_536 && text.count { it == '{' || it == '[' } <= 32) { "INVALID_STATUS" }
            val root = JSONObject(text)
            fun number(key: String, nullable: Boolean = false): Long? {
                require(root.has(key)) { "INVALID_STATUS" }
                if (root.isNull(key)) { require(nullable) { "INVALID_STATUS" }; return null }
                val raw = root.get(key)
                require(raw is Number && raw.toString().matches(Regex("0|[1-9][0-9]*"))) { "INVALID_STATUS" }
                return raw.toString().toLong()
            }
            fun time(key: String, nullable: Boolean = false): Long? {
                require(root.has(key)) { "INVALID_STATUS" }
                if (root.isNull(key)) { require(nullable) { "INVALID_STATUS" }; return null }
                require(root.get(key) is String) { "INVALID_STATUS" }
                return Instant.parse(root.getString(key)).toEpochMilli()
            }
            fun id(key: String) = root.getString(key).also { require(it.matches(Regex("[0-9a-fA-F]{32}"))) { "INVALID_STATUS" } }
            require(number("version") == 1L) { "INVALID_STATUS" }
            val state = root.getString("account_status").also { require(it in setOf("active", "expired", "limit_reached")) { "INVALID_STATUS" } }
            val metering = root.getString("metering_status").also { require(it in setOf("complete", "partial", "unavailable", "not_metered")) { "INVALID_STATUS" } }
            val limit = number("limit_bytes", true)
            val used = number("used_bytes", true)
            require(if (metering == "not_metered") limit == null && used == null else limit != null && limit > 0 && used != null) { "INVALID_STATUS" }
            val revision = requireNotNull(number("revision")).also { require(it > 0) { "INVALID_STATUS" } }
            return AccountStatus(id("server_id"), id("account_id"), id("period_id"), state,
                time("expires_at", true), limit, used, metering, time("sampled_at", true),
                requireNotNull(time("generated_at")), revision)
        }
    }
}

enum class AccountSeverity { NORMAL, WARNING, CRITICAL, UNKNOWN }

/** Advance HTTP Date with elapsed time rather than trusting the phone's wall clock. */
data class AccountStatusView(val status: AccountStatus?, val serverNow: Long?, val trustworthy: Boolean,
    val severity: AccountSeverity, val error: String = "") {
    val remainingMillis: Long? get() = status?.expiresAt?.let { expiry -> serverNow?.let { (expiry - it).coerceAtLeast(0) } }
    companion object {
        fun evaluate(status: AccountStatus?, checkedAt: Long, checkedElapsed: Long, serverTime: Long,
            error: String, now: Long, elapsed: Long): AccountStatusView {
            val age = elapsed - checkedElapsed
            val sameClock = age >= 0 && abs((now - checkedAt) - age) <= 5_000
            val serverNow = if (sameClock && serverTime > 0 && age <= 86_400_000) serverTime + age else null
            // Unlimited or archived accounts need no running collection timer.
            val staticStatus = status != null && (status.state != "active" || status.limitBytes == null && status.expiresAt == null)
            val freshFile = status != null && serverNow != null && age <= 15 * 60_000 &&
                (staticStatus || serverNow - status.generatedAt in -5_000L..(15 * 60_000L))
            val measured = status != null && (status.state != "active" || status.metering == "not_metered" ||
                ((status.metering == "complete" || status.providerReported) && status.sampledAt != null && serverNow != null &&
                    serverNow - status.sampledAt in -5_000L..(15 * 60_000L)))
            val trustworthy = freshFile && measured && error.isEmpty()
            val remaining = status?.remainingBytes
            val severity = when {
                !trustworthy || status == null -> AccountSeverity.UNKNOWN
                status.state != "active" || (status.expiresAt != null && status.expiresAt <= requireNotNull(serverNow)) -> AccountSeverity.CRITICAL
                status.remainingBytes == 0L -> AccountSeverity.CRITICAL
                status.limitBytes != null && remaining != null && remaining.toDouble() / status.limitBytes <= .05 -> AccountSeverity.CRITICAL
                status.expiresAt != null && status.expiresAt - requireNotNull(serverNow) <= 86_400_000 -> AccountSeverity.CRITICAL
                status.limitBytes != null && remaining != null && remaining.toDouble() / status.limitBytes <= .20 -> AccountSeverity.WARNING
                status.expiresAt != null && status.expiresAt - requireNotNull(serverNow) <= 3 * 86_400_000L -> AccountSeverity.WARNING
                else -> AccountSeverity.NORMAL
            }
            return AccountStatusView(status, serverNow, trustworthy, severity, error)
        }
    }
}
