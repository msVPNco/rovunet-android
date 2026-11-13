package app.slipnet.data.subscription

import android.os.SystemClock
import androidx.room.Room
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.DeviceSecrets
import app.slipnet.data.local.database.SlipNetDatabase
import dagger.hilt.android.EntryPointAccessors
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.first
import org.junit.Assert.*
import org.junit.Test

class AccountStatusDeviceTest {
    @Test fun suppliedPanelStatusFixturesPersistPeriodsForBothSubscriptionAliases() = runBlocking {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val context = instrumentation.targetContext
        val subscriptions = EntryPointAccessors.fromApplication(context, SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db = Room.inMemoryDatabaseBuilder(context, SlipNetDatabase::class.java).build()
        fun key(address: String) = java.security.MessageDigest.getInstance("SHA-256").digest(address.toByteArray()).joinToString("") { "%02x".format(it) }
        val fixed = java.time.Instant.parse("2026-11-08T10:00:00Z").toEpochMilli()
        var body = ""
        var requested = ""
        val store = AccountStatusStore(db, subscriptions) { target ->
            requested = target
            SubscriptionRepository.Download(200, null, body, fixed, System.currentTimeMillis())
        }
        try {
            for (suffix in listOf(".json", ".windows.json")) {
                val id = "panel-status" + suffix
                val address = "https://subscription.example.invalid/sub/" + "a".repeat(32) + suffix
                db.subscriptionDao().put(SubscriptionEntity(id, "Fixture", DeviceSecrets.seal(address, "subscription:$id")))
                var previous: AccountStatus? = null
                for (name in listOf("active", "partial", "expired", "renewed", "limit-reached")) {
                    body = instrumentation.context.assets.open("int03-status/$name.status.json").bufferedReader().use { it.readText() }
                    val incoming = AccountStatus.read(body)
                    store.refresh(true)
                    val cached = requireNotNull(db.privateAccountStatusDao().get(id, key(address)))
                    assertEquals(incoming, AccountStatus.read(cached.payload))
                    assertEquals("", cached.error)
                    assertTrue(requested.endsWith("/" + "a".repeat(32) + ".status.json"))
                    previous?.let { assertTrue(incoming.accepts(it)) }
                    if (name == "partial") {
                        assertFalse(cached.display().view().trustworthy)
                        assertEquals(1134L, cached.display().view().status?.usedBytes)
                    }
                    previous = incoming
                }
                db.subscriptionDao().delete(id)
            }
        } finally { db.close() }
    }

    @Test fun metadataCacheSurvivesSourceUpdatesAndRejectsDetachedOrReplacedSources() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val subscriptions = EntryPointAccessors.fromApplication(context, SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db = Room.inMemoryDatabaseBuilder(context, SlipNetDatabase::class.java).build()
        fun key(address: String) = java.security.MessageDigest.getInstance("SHA-256").digest(address.toByteArray()).joinToString("") { "%02x".format(it) }
        val id = "status-fixture"
        val address = "https://example.invalid/sub/" + "a".repeat(32) + ".json"
        var source = SubscriptionEntity(id, "Fixture", DeviceSecrets.seal(address, "subscription:$id"))
        val now = System.currentTimeMillis()
        var response = SubscriptionRepository.Download(200, null, AccountStatus("b".repeat(32), "c".repeat(32),
            "d".repeat(32), "active", null, null, null, "not_metered", null, now, 1).json(), now, now)
        var requested: String? = null
        var wait: CompletableDeferred<Unit>? = null
        var entered = CompletableDeferred<Unit>()
        val store = AccountStatusStore(db, subscriptions) { target ->
            requested = target; entered.complete(Unit); wait?.await(); response
        }
        try {
            db.subscriptionDao().put(source)
            store.refresh(true)
            assertEquals(address.removeSuffix(".json") + ".status.json", requested)
            var first = requireNotNull(db.privateAccountStatusDao().get(id, key(address)))
            assertTrue(first.display().view().trustworthy)
            source = source.copy(name = "Renamed", autoUpdate = true, lastSuccess = 100)
            db.subscriptionDao().put(source)
            assertEquals("Ordinary subscription updates must not cascade-delete status", first, db.privateAccountStatusDao().get(id, key(address)))

            source = source.copy(lastSuccess = first.checkedAt + 1)
            db.subscriptionDao().put(source)
            response = response.copy(text = AccountStatus.read(first.payload).copy(revision = 2).json())
            store.refresh(false)
            first = requireNotNull(db.privateAccountStatusDao().get(id, key(address)))
            assertEquals("An explicit feed update bypasses the metadata cooldown", 2L, AccountStatus.read(first.payload).revision)

            response = SubscriptionRepository.Download(404, null, "")
            store.refresh(true)
            val missing = requireNotNull(db.privateAccountStatusDao().get(id, key(address)))
            assertEquals(first.payload, missing.payload)
            assertFalse("A 404 must never become an exhausted or expired account", missing.display().view().trustworthy)

            response = SubscriptionRepository.Download(200, null, first.payload, now, now)
            wait = CompletableDeferred(); entered = CompletableDeferred()
            val refresh = launch { store.refresh(true) }
            withTimeout(5000) { entered.await() }
            val changed = "https://example.invalid/sub/" + "e".repeat(32) + ".json"
            source = source.copy(sourceEnvelope = DeviceSecrets.seal(changed, "subscription:$id"))
            db.subscriptionDao().put(source)
            wait!!.complete(Unit); refresh.join()
            assertEquals("SOURCE_CHANGED", withTimeout(5000) { store.observe().first() }.single().error)
            assertEquals(first.payload, requireNotNull(db.privateAccountStatusDao().get(id, key(address))).payload)
            source = source.copy(sourceEnvelope = DeviceSecrets.seal(address, "subscription:$id"))
            db.subscriptionDao().put(source)

            wait = CompletableDeferred(); entered = CompletableDeferred()
            val detached = launch { store.refresh(true) }
            withTimeout(5000) { entered.await() }
            db.subscriptionDao().delete(id)
            wait!!.complete(Unit); detached.join()
            assertNull(db.privateAccountStatusDao().get(id, key(address)))
            assertEquals(store.endpoint(address), store.endpoint(address.removeSuffix(".json") + ".windows.json"))
            assertNull(store.endpoint("https://example.invalid/ordinary.windows.json"))
            assertNull(store.endpoint("https://example.invalid/ordinary.json"))
            assertNull(store.endpoint(address.replace("https:", "http:")))
        } finally { db.close() }
    }
}
