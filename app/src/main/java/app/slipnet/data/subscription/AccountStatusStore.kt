package app.slipnet.data.subscription

import android.os.SystemClock
import androidx.room.withTransaction
import app.slipnet.data.local.database.SlipNetDatabase
import app.slipnet.util.scanWorkers
import java.security.MessageDigest
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.sync.Mutex
import okhttp3.HttpUrl.Companion.toHttpUrl

/** No periodic worker or wake lock. The visible screen owns and cancels refreshes. */
internal class AccountStatusStore(private val db: SlipNetDatabase, private val subscriptions: SubscriptionRepository,
    private val download: suspend (String) -> SubscriptionRepository.Download = subscriptions::fetchAccountStatus) {
    private val refreshLock = Mutex()
    private fun fingerprint(address: String) = MessageDigest.getInstance("SHA-256").digest(address.toByteArray())
        .joinToString("") { "%02x".format(it) }

    private fun address(row: SubscriptionEntity): String = subscriptions.sourceUrl(row).toHttpUrl().toString()
    internal fun endpoint(address: String): String? = PrivateStatusPolicy.endpoint(address)

    fun observe(): Flow<List<AccountStatusEntity>> = combine(db.subscriptionDao().observe(), db.accountStatusDao().observe(), db.privateAccountStatusDao().observe()) { sources, cached, privateRows ->
        val keys = sources.mapNotNull { source -> runCatching { source.id to fingerprint(address(source)) }.getOrNull() }.toMap()
        val knownPrivateSources = sources.filter { source -> runCatching { endpoint(address(source)) != null }.getOrDefault(false) }.map { it.id }.toSet()
        val private = privateRows.filter { it.subscriptionId in knownPrivateSources && keys[it.subscriptionId] == it.sourceFingerprint }.map { it.display() }
        val changed = sources.filter { source -> privateRows.any { it.subscriptionId == source.id } && private.none { it.subscriptionId == source.id } }
            .map { source -> AccountStatusEntity(source.id, keys[source.id].orEmpty(), "", 0, 0, 0, "SOURCE_CHANGED") }
        // A failed/stale private response cannot be made active by a public header.
        private + changed + cached.filter { keys[it.subscriptionId] == it.sourceFingerprint && (private + changed).none { row -> row.subscriptionId == it.subscriptionId } }
    }.flowOn(Dispatchers.IO)

    suspend fun refresh(force: Boolean) = withContext(Dispatchers.IO) {
        if (!refreshLock.tryLock()) return@withContext
        try {
            scanWorkers(db.subscriptionDao().all(), 2) { source ->
                val original = runCatching { address(source) }.getOrNull() ?: return@scanWorkers
                val target = endpoint(original) ?: return@scanWorkers
                val key = fingerprint(original)
                val cached = db.privateAccountStatusDao().get(source.id, key)
                if (cached == null && db.privateAccountStatusDao().hasHistory(source.id)) return@scanWorkers
                val elapsed = SystemClock.elapsedRealtime()
                val now = System.currentTimeMillis()
                val age = cached?.let { elapsed - it.checkedElapsed }
                if (!force && age != null && source.lastSuccess <= cached.checkedAt && age in 0..120_000 &&
                    kotlin.math.abs((now - cached.checkedAt) - age) <= 5_000) return@scanWorkers
                var safeError = "UNAVAILABLE"
                val refreshed = try {
                    val response = download(target)
                    require(response.code == 200) { "UNAVAILABLE" }
                    val status = AccountStatus.read(response.text)
                    val received = System.currentTimeMillis()
                    val transferTime = (received - response.receivedAt).coerceIn(0, 25_000)
                    PrivateAccountStatusEntity(source.id, key, status.serverId, status.accountId, status.revision,
                        status.json(), received, SystemClock.elapsedRealtime(),
                        if (response.serverTime > 0) response.serverTime + transferTime else 0, "")
                } catch (cancelled: CancellationException) { throw cancelled
                } catch (error: Exception) {
                    safeError = error.message?.takeIf { it in setOf("UNAVAILABLE", "UNTRUSTED_STATUS") } ?: "UNAVAILABLE"
                    null
                }
                db.withTransaction {
                    // A detached source or an edited URL must not receive an old response.
                    val current = db.subscriptionDao().get(source.id)
                    if (current != null && runCatching { fingerprint(address(current)) == key }.getOrDefault(false)) {
                        val latest = db.privateAccountStatusDao().get(source.id, key)
                        if (refreshed != null) {
                            val incoming = AccountStatus.read(refreshed.payload)
                            val previous = latest?.let { runCatching { AccountStatus.read(it.payload) }.getOrNull() }
                            // Re-read identity and high-water under the commit transaction: a
                            // snapshot taken before HTTP cannot authorize a concurrent rollback.
                            if (PrivateStatusPolicy.accepts(latest?.binding(), key, incoming, previous,
                                    latest == null && db.privateAccountStatusDao().hasHistory(source.id)))
                                db.privateAccountStatusDao().put(refreshed)
                        } else if (latest != null && latest == cached) {
                            // Keep the original good receipt and the independent high-water.
                            db.privateAccountStatusDao().put(latest.copy(error = safeError))
                        } else if (latest == null && db.accountStatusDao().get(source.id) == null) {
                            db.accountStatusDao().put(AccountStatusEntity(source.id, key, "", 0, 0, 0, safeError))
                        }
                    }
                }
            }
        } finally { refreshLock.unlock() }
    }
}
