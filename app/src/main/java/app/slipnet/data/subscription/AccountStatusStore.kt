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
    internal fun endpoint(address: String): String? {
        val url = address.toHttpUrl()
        if (!url.isHttps || url.username.isNotEmpty() || url.password.isNotEmpty() || url.fragment != null) return null
        val last = url.pathSegments.lastOrNull() ?: return null
        // Only the panel's documented sibling URL; no probing arbitrary imported formats.
        if (url.pathSegments.dropLast(1).lastOrNull() != "sub" || !last.matches(Regex("[0-9a-fA-F]{32}\\.json"))) return null
        return url.newBuilder().setPathSegment(url.pathSegments.lastIndex, last.removeSuffix(".json") + ".status.json").build().toString()
    }

    fun observe(): Flow<List<AccountStatusEntity>> = combine(db.subscriptionDao().observe(), db.accountStatusDao().observe()) { sources, cached ->
        val keys = sources.mapNotNull { source -> runCatching { source.id to fingerprint(address(source)) }.getOrNull() }.toMap()
        cached.filter { keys[it.subscriptionId] == it.sourceFingerprint }
    }.flowOn(Dispatchers.IO)

    suspend fun refresh(force: Boolean) = withContext(Dispatchers.IO) {
        if (!refreshLock.tryLock()) return@withContext
        try {
            scanWorkers(db.subscriptionDao().all(), 2) { source ->
                val original = runCatching { address(source) }.getOrNull() ?: return@scanWorkers
                val target = endpoint(original) ?: return@scanWorkers
                val key = fingerprint(original)
                val cached = db.accountStatusDao().get(source.id)?.takeIf { it.sourceFingerprint == key }
                val elapsed = SystemClock.elapsedRealtime()
                val now = System.currentTimeMillis()
                val age = cached?.let { elapsed - it.checkedElapsed }
                if (!force && age != null && source.lastSuccess <= cached.checkedAt && age in 0..120_000 &&
                    kotlin.math.abs((now - cached.checkedAt) - age) <= 5_000) return@scanWorkers
                val refreshed = try {
                    val response = download(target)
                    require(response.code != 404) { "UNAVAILABLE" }
                    val status = AccountStatus.read(response.text)
                    val previous = cached?.let { runCatching { AccountStatus.read(it.payload) }.getOrNull() }
                    require(previous == null || status.accepts(previous)) { "OLDER" }
                    val received = System.currentTimeMillis()
                    val transferTime = (received - response.receivedAt).coerceIn(0, 25_000)
                    AccountStatusEntity(source.id, key, status.json(), received, SystemClock.elapsedRealtime(),
                        if (response.serverTime > 0) response.serverTime + transferTime else 0, "")
                } catch (cancelled: CancellationException) { throw cancelled
                } catch (error: Exception) {
                    val safe = error.message?.takeIf { it in setOf("UNAVAILABLE", "OLDER") } ?: "UNAVAILABLE"
                    if (cached?.view()?.status?.providerReported == true) cached else
                        AccountStatusEntity(source.id, key, cached?.payload.orEmpty(), System.currentTimeMillis(),
                            SystemClock.elapsedRealtime(), 0, safe)
                }
                db.withTransaction {
                    // A detached source or an edited URL must not receive an old response.
                    val current = db.subscriptionDao().get(source.id)
                    if (current != null && runCatching { fingerprint(address(current)) == key }.getOrDefault(false))
                        db.accountStatusDao().put(refreshed)
                }
            }
        } finally { refreshLock.unlock() }
    }
}
