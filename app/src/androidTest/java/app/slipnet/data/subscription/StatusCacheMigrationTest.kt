package app.slipnet.data.subscription

import android.content.ContentValues
import androidx.room.Room
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.database.SlipNetDatabase
import java.util.UUID
import kotlinx.coroutines.runBlocking
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class StatusCacheMigrationTest {
    @Test fun actualSchema50MigratesPrivateBindingAndAdvisorySeparatelyWithoutDeletingRows() = runBlocking<Unit> {
        val inst = InstrumentationRegistry.getInstrumentation()
        val context = inst.targetContext
        val name = "status-migration-${UUID.randomUUID()}.db"
        val schema = JSONObject(inst.context.assets.open("app.slipnet.data.local.database.SlipNetDatabase/50.json").bufferedReader().use { it.readText() }).getJSONObject("database")
        val private = AccountStatus("a".repeat(32), "b".repeat(32), "c".repeat(32), "active", null,
            1073741824, 1134, "complete", 1794132000000, 1794132000000, 10)
        val advisory = JSONObject().put("source", "subscription-userinfo").put("used", 30).put("limit", 100)
            .put("volume_known", true).put("expiry", JSONObject.NULL).put("expiry_known", false)
            .put("received", 1794132000000).toString() // Actual legacy provider-cache format from schema50/code43.
        try {
            context.openOrCreateDatabase(name, 0, null).use { old ->
                val entities = schema.getJSONArray("entities")
                for (i in 0 until entities.length()) {
                    val e = entities.getJSONObject(i)
                    old.execSQL(e.getString("createSql").replace("\${TABLE_NAME}", e.getString("tableName")))
                }
                val setup = schema.getJSONArray("setupQueries")
                for (i in 0 until setup.length()) old.execSQL(setup.getString(i))
                for ((id, payload) in listOf("private" to private.json(), "provider" to advisory, "broken" to "malformed-preserved")) {
                    old.execSQL("INSERT INTO subscriptions VALUES (?, ?, ?, ?, ?, ?, ?)", arrayOf(id, "Synthetic", "sealed-synthetic", "[]", 123L, "", 0))
                    old.execSQL("INSERT INTO account_status VALUES (?, ?, ?, ?, ?, ?, ?)", arrayOf(id, "fingerprint", payload, 111L, 222L, 333L, ""))
                }
                val profile = (0 until entities.length()).map { entities.getJSONObject(it) }.single { it.getString("tableName") == "server_profiles" }
                val values = ContentValues()
                val fields = profile.getJSONArray("fields")
                for (i in 0 until fields.length()) {
                    val field = fields.getJSONObject(i)
                    if (field.getBoolean("notNull") && !field.has("defaultValue")) {
                        if (field.getString("affinity") == "INTEGER") values.put(field.getString("columnName"), 0)
                        else values.put(field.getString("columnName"), "")
                    }
                }
                values.put("id", 7); values.put("name", "Preserve me"); values.put("secretEnvelope", "sealed-synthetic-profile")
                old.insertOrThrow("server_profiles", null, values)
                old.version = 50
            }
            var copyReached = false
            val interruptedMigration = object : androidx.room.migration.Migration(50, 51) {
                override fun migrate(db: androidx.sqlite.db.SupportSQLiteDatabase) {
                    SlipNetDatabase.MIGRATION_50_51.migrate(db)
                    copyReached = true
                    error("Synthetic failure after copy; Room must roll back the whole migration")
                }
            }
            val interrupted = Room.databaseBuilder(context, SlipNetDatabase::class.java, name).addMigrations(interruptedMigration).build()
            try { assertTrue(runCatching { interrupted.openHelper.writableDatabase }.isFailure) }
            finally { interrupted.close() }
            assertTrue("The failure must happen after the actual copy", copyReached)
            context.openOrCreateDatabase(name, 0, null).use { old ->
                assertEquals(50, old.version)
                old.rawQuery("SELECT payload FROM account_status WHERE subscriptionId='private'", null).use { row ->
                    assertTrue(row.moveToFirst()); assertEquals(private.json(), row.getString(0))
                }
                old.rawQuery("SELECT COUNT(*) FROM account_status", null).use { row -> assertTrue(row.moveToFirst()); assertEquals(3, row.getInt(0)) }
                old.rawQuery("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='private_account_status'", null).use { row ->
                    assertTrue(row.moveToFirst()); assertEquals(0, row.getInt(0))
                }
            }
            fun open() = Room.databaseBuilder(context, SlipNetDatabase::class.java, name).addMigrations(SlipNetDatabase.MIGRATION_50_51).build()
            var db = open()
            try {
                val migrated = requireNotNull(db.privateAccountStatusDao().get("private", "fingerprint"))
                assertEquals(private, AccountStatus.read(migrated.payload))
                assertEquals(10L, migrated.highWater); assertEquals(private.serverId, migrated.serverId); assertEquals(private.accountId, migrated.accountId)
                assertEquals(111L, migrated.checkedAt); assertEquals(222L, migrated.checkedElapsed); assertEquals(333L, migrated.serverTime)
                assertNull(db.accountStatusDao().get("private"))
                assertEquals(advisory, db.accountStatusDao().get("provider")!!.payload)
                assertEquals("malformed-preserved", db.accountStatusDao().get("broken")!!.payload)
                assertEquals(3, db.subscriptionDao().all().size)
                db.openHelper.readableDatabase.query("SELECT name, secretEnvelope FROM server_profiles WHERE id=7").use { row ->
                    assertTrue(row.moveToFirst()); assertEquals("Preserve me", row.getString(0)); assertEquals("sealed-synthetic-profile", row.getString(1))
                }
                db.close(); db = open()
                assertEquals(migrated, db.privateAccountStatusDao().get("private", "fingerprint"))
                assertTrue(db.privateAccountStatusDao().hasHistory("private"))
            } finally { db.close() }
        } finally { context.deleteDatabase(name) }
    }
}
