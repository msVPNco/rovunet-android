package app.slipnet.security

import android.content.ContentValues
import android.os.Looper
import android.os.SystemClock
import androidx.room.Room
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.database.SlipNetDatabase
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.data.repository.ProfileRepositoryImpl
import com.google.gson.Gson
import java.util.UUID
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withContext
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class MigrationPerformanceTest {
    @Test fun firstProfileReadMigratesOffMainAndPreservesEveryCredential() = runBlocking<Unit> {
        val inst = InstrumentationRegistry.getInstrumentation()
        val context = inst.targetContext
        val count = InstrumentationRegistry.getArguments().getString("migrationSampleSize")?.toInt()?.coerceIn(2, 2000) ?: 32
        val name = "migration-performance-${UUID.randomUUID()}.db"
        val schema = JSONObject(inst.context.assets.open("app.slipnet.data.local.database.SlipNetDatabase/46.json")
            .bufferedReader().use { it.readText() }).getJSONObject("database")
        val entities = schema.getJSONArray("entities")
        context.openOrCreateDatabase(name, 0, null).use { old ->
            for (i in 0 until entities.length()) {
                val entity = entities.getJSONObject(i)
                old.execSQL(entity.getString("createSql").replace("\${TABLE_NAME}", entity.getString("tableName")))
            }
            val setup = schema.getJSONArray("setupQueries")
            for (i in 0 until setup.length()) old.execSQL(setup.getString(i))
            val fields = entities.getJSONObject(0).getJSONArray("fields")
            val row = ContentValues()
            for (i in 0 until fields.length()) {
                val field = fields.getJSONObject(i)
                if (field.getBoolean("notNull") && !field.has("defaultValue")) {
                    val column = field.getString("columnName")
                    if (field.getString("affinity") == "INTEGER") row.put(column, 0) else row.put(column, "")
                }
            }
            row.put("domain", "migration.invalid"); row.put("tunnel_type", "ssh")
            row.put("ssh_username", "device-test"); row.put("ssh_password", "synthetic-migration-password")
            old.beginTransaction()
            try {
                for (i in 1..count) {
                    row.put("id", i); row.put("name", "path-$i")
                    old.insertOrThrow("server_profiles", null, row)
                }
                old.version = 46
                old.setTransactionSuccessful()
            } finally { old.endTransaction() }
        }
        val migrating = AtomicBoolean(false)
        val beats = AtomicInteger(0)
        var elapsed = 0L
        val observedMigration = object : Migration(46, 47) {
            override fun migrate(db: SupportSQLiteDatabase) {
                assertNotEquals(Looper.getMainLooper(), Looper.myLooper())
                migrating.set(true)
                val start = SystemClock.elapsedRealtime()
                try { SlipNetDatabase.MIGRATION_46_47.migrate(db) }
                finally { elapsed = SystemClock.elapsedRealtime() - start; migrating.set(false) }
            }
        }
        val db = Room.databaseBuilder(context, SlipNetDatabase::class.java, name)
            .addMigrations(observedMigration, SlipNetDatabase.MIGRATION_47_48).build()
        val repo = ProfileRepositoryImpl(db.profileDao(), ProfileMapper(Gson()))
        val heartbeat = launch(Dispatchers.Main) {
            while (true) { if (migrating.get()) beats.incrementAndGet(); delay(50) }
        }
        try {
            // Same repository entry point used by UI callers. It must dispatch before Room opens/migrates.
            val first = withContext(Dispatchers.Main) { requireNotNull(repo.getProfileById(1)) }
            assertEquals("synthetic-migration-password", first.sshPassword)
            assertEquals("synthetic-migration-password", repo.getProfileById(count.toLong())?.sshPassword)
            assertEquals(count, db.profileDao().getProfileCount())
            db.openHelper.readableDatabase.query("SELECT COUNT(*) FROM server_profiles WHERE ssh_password<>'' OR secretEnvelope=''").use {
                assertTrue(it.moveToFirst()); assertEquals(0, it.getInt(0))
            }
            if (elapsed >= 500) assertTrue("Main thread made no progress during migration", beats.get() > 0)
            android.util.Log.i("RovunetMigrationQA", "rows=$count elapsed_ms=$elapsed main_heartbeats=${beats.get()}")
        } finally {
            heartbeat.cancel()
            db.close()
            context.deleteDatabase(name)
        }
    }
}
