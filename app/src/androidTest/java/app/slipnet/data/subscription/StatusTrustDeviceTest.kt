package app.slipnet.data.subscription

import android.content.Context
import androidx.room.Room
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.export.ConfigExporter
import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.local.DeviceSecrets
import app.slipnet.data.local.database.SlipNetDatabase
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.service.VpnConnectionManager
import com.google.gson.Gson
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.components.SingletonComponent
import java.security.MessageDigest
import java.util.UUID
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.first
import org.junit.Assert.*
import org.junit.Test

@EntryPoint
@InstallIn(SingletonComponent::class)
internal interface StatusReviewDependencies {
    fun connection(): VpnConnectionManager
    fun preferences(): PreferencesDataStore
}

internal object StatusReviewHarness {
    fun repository(context: Context, db: SlipNetDatabase): SubscriptionRepository {
        val entry = EntryPointAccessors.fromApplication(context, StatusReviewDependencies::class.java)
        return SubscriptionRepository(context, db, ProfileMapper(Gson()), ConfigImporter(), ConfigExporter(), entry.connection(), entry.preferences())
    }
}

/** Isolated databases and injected HTTP bodies; no daily profile DB or real server traffic. */
class StatusTrustDeviceTest {
    private val context get() = InstrumentationRegistry.getInstrumentation().targetContext
    private val address = "https://subscription.example.invalid/sub/" + "a".repeat(32) + ".json"
    private val link = "vless://00000000-0000-4000-8000-000000000001@example.invalid:443?security=tls&type=tcp"
    private fun key(value: String) = MessageDigest.getInstance("SHA-256").digest(value.toByteArray()).joinToString("") { "%02x".format(it) }
    private fun status() = AccountStatus("b".repeat(32), "c".repeat(32), "d".repeat(32), "active", null,
        1073741824, 1134, "complete", System.currentTimeMillis(), System.currentTimeMillis(), 10)
    private fun response(s: AccountStatus) = SubscriptionRepository.Download(200, null, s.json(), System.currentTimeMillis(), System.currentTimeMillis())

    @Test fun privateHighWaterSurvivesHealthyInvalidMissingHeadersAndReopen() = runBlocking<Unit> {
        val name = "status-review-${UUID.randomUUID()}.db"
        fun open() = Room.databaseBuilder(context, SlipNetDatabase::class.java, name).build()
        var db = open()
        try {
            var repo = StatusReviewHarness.repository(context, db)
            val initial = repo.prepareDocument("Isolated", address, null, false, link)
            repo.apply(initial)
            val id = initial.source.id
            var download = response(status())
            fun store() = AccountStatusStore(db, repo) { download }
            store().refresh(true)
            val good = requireNotNull(db.privateAccountStatusDao().get(id, key(address)))
            for (header in listOf("upload=10;download=20;total=1073741824", "total=-1", null)) {
                val preview = repo.prepareUsing("Isolated", address, id, false) { _, _ ->
                    SubscriptionRepository.Download(200, null, link, System.currentTimeMillis(), System.currentTimeMillis(), header)
                }
                repo.apply(preview)
                assertEquals(good, db.privateAccountStatusDao().get(id, key(address)))
                download = response(AccountStatus.read(good.payload).copy(revision = 9))
                store().refresh(true)
                assertEquals(good, db.privateAccountStatusDao().get(id, key(address)))
            }
            db.close(); db = open(); repo = StatusReviewHarness.repository(context, db)
            assertEquals(good, db.privateAccountStatusDao().get(id, key(address)))
            store().refresh(true)
            assertEquals(good, db.privateAccountStatusDao().get(id, key(address)))
            repo.apply(repo.prepareUsing("Isolated", address, id, false) { _, _ ->
                SubscriptionRepository.Download(200, null, link, System.currentTimeMillis(), System.currentTimeMillis(), "upload=10;download=20;total=1073741824")
            })
            val advisory = db.accountStatusDao().get(id)
            assertTrue("404/503 must not promote even a healthy advisory over private history", requireNotNull(advisory).view().trustworthy)
            for (code in listOf(404, 503)) {
                download = SubscriptionRepository.Download(code, null, "")
                store().refresh(true)
                val failed = requireNotNull(db.privateAccountStatusDao().get(id, key(address)))
                assertEquals(good.copy(error = "UNAVAILABLE"), failed)
                assertEquals(advisory, db.accountStatusDao().get(id))
                val shown = store().observe().first().single()
                assertFalse(shown.view().trustworthy); assertFalse(shown.view().status!!.providerReported)
            }
        } finally { db.close(); context.deleteDatabase(name) }
    }

    @Test fun concurrentResponsesIdentityChangesAndLateUrlResponsesCannotReplaceCommittedState() = runBlocking<Unit> {
        val db = Room.inMemoryDatabaseBuilder(context, SlipNetDatabase::class.java).build()
        val repo = StatusReviewHarness.repository(context, db)
        try {
            val initial = repo.prepareDocument("Isolated", address, null, false, link)
            repo.apply(initial)
            val id = initial.source.id
            val original = status()
            AccountStatusStore(db, repo) { response(original) }.refresh(true)
            val first = requireNotNull(db.privateAccountStatusDao().get(id, key(address)))
            for (bad in listOf(original.copy(serverId = "e".repeat(32), revision = 11), original.copy(accountId = "e".repeat(32), revision = 11))) {
                AccountStatusStore(db, repo) { response(bad) }.refresh(true)
                assertEquals(first, db.privateAccountStatusDao().get(id, key(address)))
            }
            val entered = CompletableDeferred<Unit>(); val release = CompletableDeferred<Unit>()
            val older = launch { AccountStatusStore(db, repo) { entered.complete(Unit); release.await(); response(original.copy(revision = 11)) }.refresh(true) }
            withTimeout(5000) { entered.await() }
            AccountStatusStore(db, repo) { response(original.copy(revision = 12, usedBytes = 2000)) }.refresh(true)
            val latest = requireNotNull(db.privateAccountStatusDao().get(id, key(address)))
            release.complete(Unit); older.join()
            assertEquals(latest, db.privateAccountStatusDao().get(id, key(address)))
            assertEquals(12L, latest.highWater)

            val entered2 = CompletableDeferred<Unit>(); val release2 = CompletableDeferred<Unit>()
            val late = launch { AccountStatusStore(db, repo) { entered2.complete(Unit); release2.await(); response(original.copy(revision = 13)) }.refresh(true) }
            withTimeout(5000) { entered2.await() }
            val source = requireNotNull(db.subscriptionDao().get(id))
            val changed = address.replace("a".repeat(32), "f".repeat(32))
            db.subscriptionDao().put(source.copy(sourceEnvelope = DeviceSecrets.seal(changed, "subscription:$id")))
            release2.complete(Unit); late.join()
            assertEquals(latest, db.privateAccountStatusDao().get(id, key(address)))
            var fetchedChangedSource = false
            val changedStore = AccountStatusStore(db, repo) { fetchedChangedSource = true; error("Edited URL must not auto-transfer existing trust") }
            changedStore.refresh(true)
            assertFalse(fetchedChangedSource)
            assertEquals("SOURCE_CHANGED", changedStore.observe().first().single().error)
            assertNull(db.privateAccountStatusDao().get(id, key(changed)))
            db.subscriptionDao().put(source)
            val renewed = original.copy(periodId = "e".repeat(32), usedBytes = 0, revision = 13)
            AccountStatusStore(db, repo) { response(renewed) }.refresh(true)
            assertEquals(renewed, AccountStatus.read(requireNotNull(db.privateAccountStatusDao().get(id, key(address))).payload))
            AccountStatusStore(db, repo) { response(original) }.refresh(true)
            assertEquals(13L, requireNotNull(db.privateAccountStatusDao().get(id, key(address))).highWater)
        } finally { db.close() }
    }

    @Test fun twoSourcesRetainIndependentAccountBindingsAndHighWaters() = runBlocking<Unit> {
        val db = Room.inMemoryDatabaseBuilder(context, SlipNetDatabase::class.java).build()
        val repo = StatusReviewHarness.repository(context, db)
        try {
            val otherAddress = address.replace("a".repeat(32), "f".repeat(32))
            val first = repo.prepareDocument("First synthetic source", address, null, false, link)
            val second = repo.prepareDocument("Second synthetic source", otherAddress, null, false, link)
            repo.apply(first); repo.apply(second)
            val original = status()
            val other = original.copy(accountId = "e".repeat(32), periodId = "f".repeat(32), revision = 20)
            var incomingFirst = original
            var incomingSecond = other
            val store = AccountStatusStore(db, repo) { target -> response(if (target.contains("a".repeat(32))) incomingFirst else incomingSecond) }
            store.refresh(true)
            val firstGood = requireNotNull(db.privateAccountStatusDao().get(first.source.id, key(address)))
            assertEquals(10L, firstGood.highWater)
            assertEquals(20L, requireNotNull(db.privateAccountStatusDao().get(second.source.id, key(otherAddress))).highWater)
            incomingFirst = other.copy(revision = 21)
            incomingSecond = other.copy(revision = 21, usedBytes = 2000)
            store.refresh(true)
            assertEquals(firstGood, db.privateAccountStatusDao().get(first.source.id, key(address)))
            val secondGood = requireNotNull(db.privateAccountStatusDao().get(second.source.id, key(otherAddress)))
            assertEquals(incomingSecond, AccountStatus.read(secondGood.payload))
            assertEquals(21L, secondGood.highWater)
            assertEquals(2, store.observe().first().size)
        } finally { db.close() }
    }
}
