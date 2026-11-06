package app.slipnet.security

import android.content.ContentValues
import androidx.room.Room
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.database.SlipNetDatabase
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.data.repository.ProfileRepositoryImpl
import app.slipnet.tunnel.TransportTrust
import com.google.gson.Gson
import com.jcraft.jsch.HostKeyRepository
import kotlinx.coroutines.runBlocking
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit

class PrivateCandidateTest {
    @Test fun migrateReal46DatabaseAndReopenWithoutLosingProfilesOrDeletionReview() = runBlocking<Unit> {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val context = instrumentation.targetContext
        val name = "candidate-storage-test.db"
        context.deleteDatabase(name)
        val schema = JSONObject(instrumentation.context.assets.open("app.slipnet.data.local.database.SlipNetDatabase/46.json")
            .bufferedReader().use { it.readText() }).getJSONObject("database")
        val entities = schema.getJSONArray("entities")
        val link = "[Interface]\nPrivateKey = synthetic-device-test"
        val secret = "synthetic-ssh-password"
        val mapper = ProfileMapper(Gson())
        context.openOrCreateDatabase(name, 0, null).use { old ->
            for (i in 0 until entities.length()) {
                val e = entities.getJSONObject(i)
                old.execSQL(e.getString("createSql").replace("\${TABLE_NAME}", e.getString("tableName")))
            }
            val sql = schema.getJSONArray("setupQueries")
            for (i in 0 until sql.length()) old.execSQL(sql.getString(i))
            val table = entities.getJSONObject(0).getJSONArray("fields")
            val values = ContentValues()
            for (i in 0 until table.length()) {
                val f = table.getJSONObject(i)
                if (f.getBoolean("notNull") && !f.has("defaultValue")) {
                    val c = f.getString("columnName")
                    if (f.getString("affinity") == "INTEGER") values.put(c, 0) else values.put(c, "")
                }
            }
            values.put("id", 7); values.put("name", "test"); values.put("coreLink", link); values.put("ssh_password", secret)
            assertEquals(7L, old.insertOrThrow("server_profiles", null, values))
            values.put("id", 6)
            assertEquals(6L, old.insertOrThrow("server_profiles", null, values))
            old.execSQL("CREATE TRIGGER qa_abort BEFORE UPDATE ON server_profiles WHEN OLD.id=7 " +
                "BEGIN SELECT RAISE(ABORT, 'simulated migration interruption'); END")
            old.version = 46
        }
        fun open() = Room.databaseBuilder(context, SlipNetDatabase::class.java, name)
            .addMigrations(SlipNetDatabase.MIGRATION_46_47).build()
        try {
            val interrupted = open()
            try {
                assertTrue(runCatching { interrupted.openHelper.writableDatabase }.isFailure)
            } finally { interrupted.close() }
            context.openOrCreateDatabase(name, 0, null).use { old ->
                assertEquals(46, old.version)
                old.rawQuery("SELECT ssh_password FROM server_profiles ORDER BY id", null).use { cursor ->
                    assertEquals(2, cursor.count)
                    while (cursor.moveToNext()) assertEquals(secret, cursor.getString(0))
                }
                old.rawQuery("PRAGMA table_info(server_profiles)", null).use { cursor ->
                    while (cursor.moveToNext()) assertNotEquals("secretEnvelope", cursor.getString(1))
                }
                old.execSQL("DROP TRIGGER qa_abort")
            }
            val first = open()
            try {
                val db = first
                val stored = requireNotNull(db.profileDao().getProfileById(7))
                assertEquals("", stored.coreLink); assertEquals("", stored.sshPassword)
                assertTrue(stored.secretEnvelope.isNotEmpty()); assertFalse(stored.secretEnvelope.contains(secret))
                val profile = mapper.toDomain(stored)
                assertEquals(link, profile.coreLink); assertEquals(secret, profile.sshPassword)
                db.profileDao().updateProfile(mapper.toEntity(profile.copy(name = "edited")))
            } finally { first.close() }
            val second = open()
            try {
                val db = second
                val current = mapper.toDomain(requireNotNull(db.profileDao().getProfileById(7)))
                assertEquals(link, current.coreLink); assertEquals("edited", current.name)
                val dao = db.profileDao()
                val repo = ProfileRepositoryImpl(dao, mapper)
                assertFalse(repo.deleteReviewedProfiles(listOf(current.copy(name = "stale")), listOf(7)))
                assertTrue(repo.deleteReviewedProfiles(listOf(current), listOf(7)))
            } finally { second.close() }
        } finally { context.deleteDatabase(name) }
    }

    @Test fun automaticScannerRejectsUnknownKeyDuringManualApproval() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        TransportTrust.initialize(context)
        val host = "review.invalid:22/ssh"
        val key = byteArrayOf(11, 22)
        val pool = Executors.newSingleThreadExecutor()
        TransportTrust.clear()
        try {
            TransportTrust.allowPrompt = true
            val approval = pool.submit<Int> { TransportTrust.sshKeys.check(host, key) }
            val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(5)
            while (TransportTrust.request.value == null && System.nanoTime() < deadline) Thread.sleep(10)
            val request = requireNotNull(TransportTrust.request.value)
            assertEquals(HostKeyRepository.NOT_INCLUDED, TransportTrust.scannerSshKeys.check(host, key))
            TransportTrust.answer(request, true)
            assertEquals(HostKeyRepository.OK, approval.get(5, TimeUnit.SECONDS))
            assertEquals(HostKeyRepository.OK, TransportTrust.scannerSshKeys.check(host, key))
            assertEquals(HostKeyRepository.CHANGED, TransportTrust.scannerSshKeys.check(host, byteArrayOf(22, 11)))
        } finally {
            TransportTrust.allowPrompt = false
            TransportTrust.clear()
            pool.shutdownNow()
        }
    }
}
