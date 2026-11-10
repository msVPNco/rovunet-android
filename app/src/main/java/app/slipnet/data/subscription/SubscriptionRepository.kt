package app.slipnet.data.subscription

import android.content.Context
import androidx.room.withTransaction
import app.slipnet.data.export.ConfigExporter
import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.export.ImportResult
import app.slipnet.data.local.DeviceSecrets
import app.slipnet.data.local.database.SlipNetDatabase
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.domain.model.ServerProfile
import app.slipnet.service.VpnConnectionManager
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import okhttp3.*
import okhttp3.HttpUrl.Companion.toHttpUrl
import org.json.JSONArray
import org.json.JSONObject
import java.security.MessageDigest
import java.util.UUID
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

data class SubscriptionPreview(val source: SubscriptionEntity, val expectedSource: SubscriptionEntity?,
    val profiles: List<Pair<String, ServerProfile>>, val removed: Int, val unsupported: Int = 0, val duplicates: Int = 0, val publicStats: List<PublicFeed.Stat> = emptyList(), val importWarnings: List<String> = emptyList(),
    val legacyKeys: Map<String, String> = emptyMap())

@Singleton
class SubscriptionRepository @Inject constructor(
    @ApplicationContext private val context: Context,
    private val db: SlipNetDatabase,
    private val mapper: ProfileMapper,
    private val importer: ConfigImporter,
    private val exporter: ConfigExporter,
    private val connection: VpnConnectionManager,
    private val preferences: app.slipnet.data.local.datastore.PreferencesDataStore
) {
    private val mutex = Mutex()
    private val client = OkHttpClient.Builder().followRedirects(false).followSslRedirects(false)
        .connectTimeout(10, TimeUnit.SECONDS).readTimeout(15, TimeUnit.SECONDS).callTimeout(25, TimeUnit.SECONDS).build()
    private val accountStatusStore by lazy { AccountStatusStore(db, this) }
    fun observeAccountStatus(): Flow<List<AccountStatusEntity>> = accountStatusStore.observe()
    suspend fun refreshAccountStatuses(force: Boolean = false) = accountStatusStore.refresh(force)
    val connectionState get() = connection.connectionState
    fun observe(): Flow<List<SubscriptionEntity>> = db.subscriptionDao().observe()
    private fun hash(value: String) = MessageDigest.getInstance("SHA-256").digest(value.toByteArray()).joinToString("") { "%02x".format(it) }
    private fun digest(p: ServerProfile) = hash(exporter.exportSingleProfile(p.copy(isLocked = false,
        collectionId = "", collectionName = "")))
    private fun url(value: String): HttpUrl = value.trim().toHttpUrl().also {
        require(it.isHttps && it.username.isEmpty() && it.password.isEmpty() && it.fragment == null) { "HTTPS_REQUIRED" }
    }
    fun sourceUrl(row: SubscriptionEntity): String = DeviceSecrets.open(row.sourceEnvelope, "subscription:${row.id}")
    fun host(row: SubscriptionEntity): String = runCatching { url(sourceUrl(row)).host }.getOrDefault("—")
    suspend fun findSource(address: String): SubscriptionEntity? = withContext(Dispatchers.IO) {
        val normalized = url(address).toString()
        db.subscriptionDao().all().firstOrNull { runCatching { url(sourceUrl(it)).toString() == normalized }.getOrDefault(false) }
    }
    internal data class Download(val code: Int, val location: String?, val text: String,
        val serverTime: Long = 0, val receivedAt: Long = 0)
    private suspend fun fetch(address: String, limit: Int = 2 * 1024 * 1024): String = fetchDocument(address, limit, false).text
    internal suspend fun fetchAccountStatus(address: String): Download = fetchDocument(address, 65_536, true)
    private suspend fun fetchDocument(address: String, maxBytes: Int, account: Boolean): Download {
        var target = url(address)
        val state = connection.connectionState.value
        val transport = if (state is app.slipnet.domain.model.ConnectionState.Connected) {
            require(state.chainId <= 0) { "DISCONNECT_TO_UPDATE" }
            val endpoint = if (state.profile.tunnelType == app.slipnet.domain.model.TunnelType.XRAY)
                app.slipnet.tunnel.XrayBridge.activeEndpoint ?: error("DOWNLOAD_FAILED")
            else preferences.configuredSocksEndpoint()
            client.newBuilder().proxy(java.net.Proxy.NO_PROXY)
                .socketFactory(app.slipnet.core.ProxySocketFactory(endpoint))
                .dns(object : okhttp3.Dns {
                    override fun lookup(hostname: String): List<java.net.InetAddress> =
                        listOf(java.net.InetAddress.getByAddress(hostname, byteArrayOf(0, 0, 0, 1)))
                })
                .connectionPool(okhttp3.ConnectionPool(0, 1, TimeUnit.SECONDS))
                .retryOnConnectionFailure(false).build()
        } else {
            require(state.isDisconnected) { "DISCONNECT_TO_UPDATE" }
            client
        }
        repeat(4) { redirect ->
            val result = suspendCancellableCoroutine<Download> { continuation ->
                val call = transport.newCall(Request.Builder().url(target).header("Accept", "application/json, text/plain").build())
                continuation.invokeOnCancellation { call.cancel() }
                call.enqueue(object : Callback {
                    override fun onFailure(call: Call, e: java.io.IOException) {
                        if (continuation.isActive) continuation.resumeWithException(IllegalStateException("DOWNLOAD_FAILED"))
                    }
                    override fun onResponse(call: Call, response: Response) {
                        val result = runCatching { response.use {
                            if (it.code in 300..399) Download(it.code, it.header("Location"), "")
                            else if (account && it.code == 404) Download(404, null, "")
                            else {
                                require(it.isSuccessful) { "DOWNLOAD_FAILED" }
                                require(!it.header("Content-Type").orEmpty().contains("text/html", ignoreCase = true)) { "INVALID_FEED" }
                                val body = it.body ?: error("DOWNLOAD_FAILED")
                                require(body.contentLength() <= maxBytes) { "FEED_TOO_LARGE" }
                                val text = body.byteStream().use { input ->
                                    val out = java.io.ByteArrayOutputStream(); val buffer = ByteArray(8192)
                                    while (true) {
                                        if (!continuation.isActive) throw CancellationException()
                                        val n = input.read(buffer); if (n < 0) break
                                        require(out.size() + n <= maxBytes) { "FEED_TOO_LARGE" }
                                        out.write(buffer, 0, n)
                                    }
                                    String(out.toByteArray(), Charsets.UTF_8)
                                }
                                val serverTime = runCatching {
                                    java.time.ZonedDateTime.parse(it.header("Date"), java.time.format.DateTimeFormatter.RFC_1123_DATE_TIME).toInstant().toEpochMilli()
                                }.getOrDefault(0L)
                                Download(it.code, null, text, serverTime, it.receivedResponseAtMillis)
                            }
                        } }
                        if (continuation.isActive) continuation.resumeWith(result)
                    }
                })
            }
            if (result.code !in 300..399) return result
            require(redirect < 3) { "DOWNLOAD_FAILED" }
            target = url(target.resolve(result.location ?: error("DOWNLOAD_FAILED"))?.toString() ?: error("DOWNLOAD_FAILED"))
        }
        error("DOWNLOAD_FAILED")
    }
    suspend fun prepare(name: String, address: String, existingId: String? = null, auto: Boolean = false): SubscriptionPreview = withContext(Dispatchers.IO) {
        val initial = existingId?.let { db.subscriptionDao().get(it) ?: error("SOURCE_CHANGED") }
        val document = if (initial?.id?.startsWith("public:") == true) PublicProviders.download(url(address).toString(), ::fetch) else fetch(address)
        val preview = prepareDocument(name, address, existingId, auto, document)
        require(preview.expectedSource == initial) { "SOURCE_CHANGED" }
        preview
    }

    suspend fun preparePublic(name: String, address: String, updateExisting: Boolean = false): SubscriptionPreview = withContext(Dispatchers.IO) {
        val existing = findSource(address)
        require(existing == null || (updateExisting && existing.id.startsWith("public:"))) { "SOURCE_EXISTS" }
        val document = PublicProviders.download(url(address).toString(), ::fetch)
        val preview = prepareDocument(existing?.name ?: name, address, existing?.id, false, document, publicMode = true)
        require(preview.expectedSource == existing) { "SOURCE_CHANGED" }
        preview
    }

    internal suspend fun prepareDocument(name: String, address: String, existingId: String?, auto: Boolean, document: String,
        publicMode: Boolean = false): SubscriptionPreview = withContext(Dispatchers.IO) {
        require(name.isNotBlank() && name.length <= 120) { "NAME_REQUIRED" }
        val existing = existingId?.let { db.subscriptionDao().get(it) ?: error("SOURCE_CHANGED") }
        val public = publicMode || existing?.id?.startsWith("public:") == true
        val id = existing?.id ?: (if (public) "public:" else "") + UUID.randomUUID().toString()
        val source = (existing ?: SubscriptionEntity(id, name.trim(), "")).copy(name = name.trim(),
            sourceEnvelope = DeviceSecrets.seal(url(address).toString(), "subscription:$id"), autoUpdate = auto && !public)
        val publicResult = if (public) PublicFeed.parse(document, importer, connection.getDeviceId(), allowEmpty = true) else null
        val importWarnings = linkedSetOf<String>()
        val legacyByCurrent = mutableMapOf<String, String>()
        val profiles = if (publicResult != null) publicResult.profiles.map { profile ->
            val stable = app.slipnet.domain.model.ProfileDuplicates.key(profile).copy(collectionId = "")
            hash(exporter.exportSingleProfile(stable)) to profile
        } else SubscriptionPayload.read(document).flatMap { item ->
            val parsed = importer.parseAndImport(item.link, connection.getDeviceId())
            require(parsed is ImportResult.Success && parsed.profiles.isNotEmpty() && parsed.warnings.all { it in setOf("REVIEW03_JSON_CONNECTIONS_ONLY", "REVIEW03_JSON_UNSUPPORTED") }) { "INVALID_FEED" }
            require(item.key == null || parsed.profiles.size == 1) { "INVALID_FEED" }
            importWarnings += parsed.warnings
            parsed.profiles.map { p ->
                val key = hash(item.key?.let { "remote-id:$it" } ?: SubscriptionIdentity.derived(p))
                if (item.key == null && p.tunnelType == app.slipnet.domain.model.TunnelType.VLESS)
                    legacyByCurrent[key] = hash(SubscriptionIdentity.legacy(p))
                key to p
            }
        }
        require(profiles.size in (if (public) 0 else 1)..(if (public) PublicFeed.MAX_ENTRIES else 1000) && profiles.map { it.first }.distinct().size == profiles.size) { "INVALID_FEED" }
        val old = JSONArray(existing?.membersJson ?: "[]")
        val previous = (0 until old.length()).map { old.getJSONObject(it) }.associateBy { it.getString("key") }
        val aliases = mutableMapOf<String, String>()
        val profilesByKey = profiles.toMap()
        for ((legacy, candidates) in legacyByCurrent.entries.groupBy { it.value }) {
            val saved = previous[legacy] ?: continue
            val eligible = candidates.filter { it.key !in previous }
            val target = eligible.singleOrNull() ?: run {
                val row = db.profileDao().getProfileById(saved.getLong("id"))
                val local = row?.let(mapper::toDomain)?.takeIf { !it.needsCredentials && it.collectionId == id }
                val key = local?.let { app.slipnet.domain.model.ProfileDuplicates.key(it).copy(collectionId = "") }
                eligible.singleOrNull { candidate -> profilesByKey[candidate.key]?.let {
                    app.slipnet.domain.model.ProfileDuplicates.key(it).copy(collectionId = "") == key
                } == true }
            }
            if (target != null) aliases[target.key] = legacy
        }
        val incoming = profiles.map { it.first }.toSet() + aliases.values
        val removed = (0 until old.length()).count { old.getJSONObject(it).getString("key") !in incoming }
        SubscriptionPreview(source, existing, profiles, removed, publicResult?.unsupported ?: 0, publicResult?.duplicates ?: 0, publicResult?.stats.orEmpty(), importWarnings.toList(), aliases)
    }
    /** Pure additions do not acquire the native lifecycle lock or change an active owner. */
    suspend fun apply(preview: SubscriptionPreview) = withContext(Dispatchers.IO) {
        require(preview.profiles.isNotEmpty()) { "PUBLIC_FEED_EMPTY" }
        mutex.withLock {
            if (preview.expectedSource == null) {
                applyTransaction(preview, addition = true)
            } else {
                app.slipnet.service.TunnelOperations.mutex.withLock {
                    require(connection.connectionState.value.isDisconnected && app.slipnet.service.TunnelOperations.owner == 0L) { "DISCONNECT_TO_UPDATE" }
                    applyTransaction(preview, addition = false)
                }
            }
        }
    }

    private suspend fun applyTransaction(preview: SubscriptionPreview, addition: Boolean) = db.withTransaction {
        val current = db.subscriptionDao().get(preview.source.id)
        require(current == preview.expectedSource) { "SOURCE_CHANGED" }
        if (addition) {
            val address = url(sourceUrl(preview.source)).toString()
            require(db.subscriptionDao().all().none { runCatching { url(sourceUrl(it)).toString() == address }.getOrDefault(false) }) { "SOURCE_EXISTS" }
        }
        val old = JSONArray(current?.membersJson ?: "[]")
        val previous = (0 until old.length()).map { old.getJSONObject(it) }.associateBy { it.getString("key") }.toMutableMap()
        require(preview.legacyKeys.values.distinct().size == preview.legacyKeys.size) { "SOURCE_CHANGED" }
        preview.legacyKeys.forEach { (key, legacy) ->
            require(key !in previous) { "SOURCE_CHANGED" }
            val saved = previous.remove(legacy) ?: error("SOURCE_CHANGED")
            previous[key] = JSONObject(saved.toString()).put("key", key)
        }
        var localCollectionName: String? = null
        for (saved in previous.values) {
            val row = db.profileDao().getProfileById(saved.getLong("id"))
            if (row != null && row.collectionId == preview.source.id && row.collectionName.isNotBlank()) {
                localCollectionName = row.collectionName; break
            }
        }
        val resolvedName = if (current != null && preview.source.name == current.name)
            localCollectionName ?: preview.source.name else preview.source.name
        val members = JSONArray()
        preview.profiles.forEach { (key, remote) ->
            require(addition || connection.connectionState.value.isDisconnected) { "DISCONNECT_TO_UPDATE" }
            val saved = previous[key]
            val row = saved?.let { db.profileDao().getProfileById(it.getLong("id")) }
            val local = row?.let(mapper::toDomain)
            // A locally edited/locked row is retained in full, never silently overwritten.
            if (local != null && (local.needsCredentials || local.collectionId != preview.source.id || local.isLocked || digest(local) != saved?.getString("digest"))) {
                if (local.collectionId == preview.source.id) db.profileDao().updateMembership(local.id, local.collectionId, resolvedName)
                members.put(saved)
            } else {
                val profile = remote.copy(id = row?.id ?: 0, collectionId = preview.source.id,
                    collectionName = resolvedName, isActive = local?.isActive ?: false,
                    isPinned = local?.isPinned ?: false, sortOrder = local?.sortOrder ?: 0,
                    createdAt = local?.createdAt ?: remote.createdAt)
                val newDigest = digest(profile)
                val id = if (row == null) db.profileDao().insertProfile(mapper.toEntity(profile))
                    else {
                        if (local != null && digest(local) == newDigest) {
                            if (row.collectionId != profile.collectionId || row.collectionName != profile.collectionName)
                                db.profileDao().updateMembership(row.id, profile.collectionId, profile.collectionName)
                        } else db.profileDao().updateProfile(mapper.toEntity(profile))
                        row.id
                    }
                members.put(JSONObject().put("key", key).put("id", id).put("digest", newDigest))
            }
        }
        val incoming = preview.profiles.map { it.first }.toSet()
        // Refresh never deletes a saved profile. Retired source entries become
        // a normal local collection and retain their row, secrets and chain IDs.
        previous.filterKeys { it !in incoming }.values.forEach { saved ->
            require(connection.connectionState.value.isDisconnected) { "DISCONNECT_TO_UPDATE" }
            val row = db.profileDao().getProfileById(saved.getLong("id"))
            if (row != null && row.collectionId == preview.source.id) {
                val suffix = app.slipnet.presentation.theme.RovunetStrings.localizedContext(context)
                    .getString(app.slipnet.R.string.subscription_retained)
                db.profileDao().updateMembership(row.id, preview.source.id + ":retained", resolvedName + " · " + suffix)
            }
        }
        db.subscriptionDao().put(preview.source.copy(name = resolvedName, membersJson = members.toString(), lastSuccess = System.currentTimeMillis(), lastError = ""))
    }
    suspend fun refresh(id: String) {
        val row = db.subscriptionDao().get(id) ?: return
        try {
            val preview = prepare(row.name, sourceUrl(row), row.id, row.autoUpdate)
            require(preview.removed == 0 && !row.id.startsWith("public:")) { "REVIEW_REQUIRED" }
            apply(preview)
        }
        catch (e: CancellationException) { throw e }
        catch (e: Exception) {
            val code = e.message?.takeIf { it in setOf("REVIEW_REQUIRED", "DISCONNECT_TO_UPDATE", "INVALID_FEED", "UNSUPPORTED_FEED", "HTTPS_REQUIRED", "FEED_TOO_LARGE") } ?: "DOWNLOAD_FAILED"
            mutex.withLock { db.subscriptionDao().get(id)?.takeIf { it == row }?.let { db.subscriptionDao().put(it.copy(lastError = code)) } }
            throw IllegalStateException(code)
        }
    }
    suspend fun detach(id: String) = mutex.withLock { db.subscriptionDao().delete(id) }
    suspend fun setAutomatic(id: String, enabled: Boolean) = mutex.withLock {
        db.subscriptionDao().get(id)?.let { db.subscriptionDao().put(it.copy(autoUpdate = enabled && !id.startsWith("public:"))) }
    }
    suspend fun refreshAutomatic() {
        if (!connection.connectionState.value.isDisconnected) return
        db.subscriptionDao().all().filter { it.autoUpdate && !it.id.startsWith("public:") }.forEach { row ->
        try { refresh(row.id) } catch (e: CancellationException) { throw e } catch (_: Exception) { /* Per-source safe code persisted. */ }
    } }
}
