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
    @Test fun metadataCacheSurvivesSourceUpdatesAndRejectsDetachedOrReplacedSources() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val subscriptions = EntryPointAccessors.fromApplication(context, SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db = Room.inMemoryDatabaseBuilder(context, SlipNetDatabase::class.java).build()
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
            val first = requireNotNull(db.accountStatusDao().get(id))
            assertTrue(first.view().trustworthy)
            source = source.copy(name = "Renamed", autoUpdate = true, lastSuccess = 100)
            db.subscriptionDao().put(source)
            assertEquals("Ordinary subscription updates must not cascade-delete status", first, db.accountStatusDao().get(id))

            response = SubscriptionRepository.Download(404, null, "")
            store.refresh(true)
            val missing = requireNotNull(db.accountStatusDao().get(id))
            assertEquals(first.payload, missing.payload)
            assertFalse("A 404 must never become an exhausted or expired account", missing.view().trustworthy)

            response = SubscriptionRepository.Download(200, null, first.payload, now, now)
            wait = CompletableDeferred(); entered = CompletableDeferred()
            val refresh = launch { store.refresh(true) }
            withTimeout(5000) { entered.await() }
            val changed = "https://example.invalid/sub/" + "e".repeat(32) + ".json"
            source = source.copy(sourceEnvelope = DeviceSecrets.seal(changed, "subscription:$id"))
            db.subscriptionDao().put(source)
            wait!!.complete(Unit); refresh.join()
            assertTrue("Old-token metadata must not attach to the edited source", withTimeout(5000) { store.observe().first() }.isEmpty())

            wait = CompletableDeferred(); entered = CompletableDeferred()
            val detached = launch { store.refresh(true) }
            withTimeout(5000) { entered.await() }
            db.subscriptionDao().delete(id)
            wait!!.complete(Unit); detached.join()
            assertNull(db.accountStatusDao().get(id))
            assertNull(store.endpoint("https://example.invalid/ordinary.json"))
            assertNull(store.endpoint(address.replace("https:", "http:")))
        } finally { db.close() }
    }
}
