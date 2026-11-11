package app.slipnet.security

import android.content.Intent
import androidx.lifecycle.ViewModelProvider
import androidx.test.core.app.ActivityScenario
import app.slipnet.di.DatabaseModule
import app.slipnet.presentation.MainActivity
import app.slipnet.presentation.profiles.EditProfileViewModel
import kotlinx.coroutines.withTimeout
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.room.Room
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.export.ImportResult
import app.slipnet.data.export.ConfigExporter
import app.slipnet.data.local.database.ProfileSecrets
import app.slipnet.data.local.database.SlipNetDatabase
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.data.repository.ProfileRepositoryImpl
import app.slipnet.domain.model.ProfileDuplicates
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import com.google.gson.Gson
import java.io.File
import java.security.KeyStore
import java.util.UUID
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Test

class CredentialRecoveryTest {
    private val context get() = InstrumentationRegistry.getInstrumentation().targetContext
    private val mapper = ProfileMapper(Gson())
    private fun example(name: String) = ServerProfile(name = name, domain = "server.invalid", tunnelType = TunnelType.SSH,
        sshUsername = "test", sshPassword = "sample-pass", collectionId = "one", collectionName = "Group", sortOrder = 3)

    /** Simulate restoring ciphertext from a different installation, without touching the app's own key. */
    private fun foreignEnvelope(purpose: String): String {
        val alias = "qa-rovunet-" + UUID.randomUUID()
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        return try {
            val generator = KeyGenerator.getInstance("AES", "AndroidKeyStore")
            generator.init(KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build())
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.ENCRYPT_MODE, generator.generateKey())
            cipher.updateAAD(purpose.toByteArray())
            "1." + Base64.encodeToString(cipher.iv + cipher.doFinal("""{"ssh_password":"previous"}""".toByteArray()), Base64.NO_WRAP)
        } finally { store.deleteEntry(alias) }
    }

    @Test fun damagedRowIsIsolatedWritesPreserveItAndExplicitRepairKeepsItsIdentity() = runBlocking<Unit> {
        val db = Room.inMemoryDatabaseBuilder(context, SlipNetDatabase::class.java).build()
        try {
            val dao = db.profileDao()
            val repo = ProfileRepositoryImpl(dao, mapper)
            val source = example("damaged")
            val good = example("healthy")
            val id = repo.saveProfile(source)
            val goodId = repo.saveProfile(good)
            val previous = requireNotNull(repo.getProfileById(id))
            val stored = requireNotNull(dao.getProfileById(id))
            val foreign = foreignEnvelope("profile-credentials-v1")
            dao.updateProfile(stored.copy(secretEnvelope = foreign))
            dao.activateProfile(id)
            val all = repo.getAllProfiles().first()
            val unavailable = all.single { it.id == id }
            assertTrue(unavailable.needsCredentials && unavailable.sshPassword.isEmpty())
            assertTrue(repo.getActiveProfile().first()!!.needsCredentials)
            assertEquals(good.sshPassword, all.single { it.id == goodId }.sshPassword)
            assertTrue(ProfileDuplicates.plan(listOf(unavailable, unavailable.copy(id = goodId))).isEmpty())
            val exporter = ConfigExporter()
            assertTrue(runCatching { exporter.exportSingleProfile(unavailable) }.isFailure)
            assertTrue(runCatching { exporter.exportAllProfiles(all) }.isFailure)
            assertTrue(runCatching { exporter.exportCollection(all, unavailable) }.isFailure)
            assertTrue(exporter.exportSingleProfile(all.single { it.id == goodId }).isNotEmpty())
            assertTrue(runCatching { repo.updateProfile(previous.copy(name = "stale editor")) }.isFailure)
            assertTrue(runCatching { repo.updateProfile(unavailable) }.isFailure)
            repo.updateCollectionMemberships(listOf(unavailable.copy(collectionName = "Renamed")))
            repo.togglePinned(id)
            dao.updateSortOrder(id, 8)
            assertEquals(foreign, dao.getProfileById(id)?.secretEnvelope)
            assertFalse(repo.restoreCredentials(unavailable, source.copy(sshUsername = "another")))
            assertFalse(repo.restoreCredentials(unavailable, source.copy(isLocked = true)))
            assertFalse(repo.restoreCredentials(unavailable, source.copy(sshPassword = "")))
            assertEquals(foreign, dao.getProfileById(id)?.secretEnvelope)
            val link = exporter.exportSingleProfile(source.copy(sshPassword = "re-entered"))
            val original = (ConfigImporter().parseAndImport(link) as ImportResult.Success).profiles.single()
            assertTrue(repo.restoreCredentials(unavailable, original))
            val fixed = requireNotNull(repo.getProfileById(id))
            assertFalse(fixed.needsCredentials)
            assertEquals("re-entered", fixed.sshPassword)
            assertEquals("Renamed", fixed.collectionName)
            assertTrue(fixed.isPinned && fixed.isActive)
            assertEquals(8, fixed.sortOrder)
            assertFalse(repo.restoreCredentials(unavailable, source))
            assertEquals("", dao.getProfileById(id)?.sshPassword)
            assertNotEquals(foreign, dao.getProfileById(id)?.secretEnvelope)
        } finally { db.close() }
    }

    @Test fun editorRecoversTheExistingRowFromAnOriginalLink() = runBlocking<Unit> {
        val db = DatabaseModule.provideDatabase(context)
        val original = example("recovery editor")
        val raw = mapper.toEntity(original)
        val id = db.profileDao().insertProfile(raw.copy(secretEnvelope = "unreadable"))
        try {
            val intent = Intent(context, MainActivity::class.java).putExtra("profileId", id)
            ActivityScenario.launch<MainActivity>(intent).use { screen ->
                lateinit var editor: EditProfileViewModel
                screen.onActivity { editor = ViewModelProvider(it)[EditProfileViewModel::class.java] }
                withTimeout(30_000) { editor.uiState.first { it.needsCredentials } }
                screen.onActivity { editor.save(); editor.retryCredentials() }
                withTimeout(30_000) { editor.uiState.first { it.needsCredentials && !it.isLoading } }
                assertEquals("unreadable", db.profileDao().getProfileById(id)?.secretEnvelope)
                val link = ConfigExporter().exportSingleProfile(original.copy(sshPassword = "restored-via-editor"))
                screen.onActivity { editor.restoreCredentials(link) }
                withTimeout(30_000) { editor.uiState.first { it.saveSuccess || it.error != null } }
                assertTrue(editor.uiState.value.error, editor.uiState.value.saveSuccess)
                assertEquals("restored-via-editor", mapper.toDomain(db.profileDao().getProfileById(id)!!).sshPassword)
            }
        } finally { db.profileDao().deleteProfile(id); db.close() }
    }

    @Test fun staleEnvelopeAndLockedConfigRejectReplacementAndStaleDeletion() = runBlocking<Unit> {
        val db = Room.inMemoryDatabaseBuilder(context, SlipNetDatabase::class.java).build()
        try {
            val dao = db.profileDao()
            val repo = ProfileRepositoryImpl(dao, mapper)
            val source = example("locked").copy(isLocked = true, lockPasswordHash = "synthetic", allowSharing = false)
            val id = repo.saveProfile(source)
            val raw = requireNotNull(dao.getProfileById(id))
            dao.updateProfile(raw.copy(secretEnvelope = "1.invalid"))
            val reviewed = requireNotNull(repo.getProfileById(id))
            assertTrue(reviewed.needsCredentials && reviewed.isLocked && !reviewed.allowSharing)
            assertTrue(ProfileSecrets.open(requireNotNull(dao.getProfileById(id))).isFailure)
            assertFalse(repo.restoreCredentials(reviewed, source.copy(isLocked = false)))
            dao.updateProfile(raw.copy(secretEnvelope = "1.other"))
            assertFalse(repo.deleteReviewedProfiles(listOf(reviewed), listOf(id)))
            assertEquals("1.other", dao.getProfileById(id)?.secretEnvelope)
            // If the original key/data becomes readable, retry needs no writes, and the old review is invalid.
            dao.updateProfile(raw)
            assertFalse(requireNotNull(repo.getProfileById(id)).needsCredentials)
            assertFalse(repo.deleteReviewedProfiles(listOf(reviewed), listOf(id)))
            assertEquals("synthetic", repo.getProfileById(id)?.lockPasswordHash)
        } finally { db.close() }
    }

    @Test fun batchDeletionChecksTheWholeReviewBeforeWriting() = runBlocking<Unit> {
        val count = InstrumentationRegistry.getArguments().getString("deleteSampleSize")?.toInt()?.coerceIn(2, 1000) ?: 16
        val db = Room.inMemoryDatabaseBuilder(context, SlipNetDatabase::class.java).build()
        try {
            val dao = db.profileDao()
            val repo = ProfileRepositoryImpl(dao, mapper)
            // One installed key, one sealed template, many different row IDs. Setup is outside the timed section.
            val template = mapper.toEntity(example("group member"))
            val ids = dao.insertCollection(List(count) { template.copy(name = "path-$it") })
            val preview = repo.getAllProfiles().first()
            dao.updateMembership(ids.last(), "different", "Renamed")
            assertFalse(repo.deleteReviewedProfiles(preview, ids))
            assertEquals(count, dao.getProfileCount())
            val reviewed = repo.getAllProfiles().first()
            // A changed secret with an unchanged timestamp and metadata invalidates the old review.
            val last = requireNotNull(dao.getProfileById(ids.last()))
            dao.updateProfile(ProfileSecrets.seal(ProfileSecrets.open(last).getOrThrow().copy(sshPassword = "rotated")))
            assertFalse(repo.deleteReviewedProfiles(reviewed, ids))
            assertEquals(count, dao.getProfileCount())
            val latest = repo.getAllProfiles().first()
            // Mutating a copied preview cannot reuse its original review capability either.
            assertFalse(repo.deleteReviewedProfiles(latest.mapIndexed { index, p -> if (index == 0) p.copy(sshPassword = "unsaved") else p }, ids))
            val start = android.os.SystemClock.elapsedRealtime()
            assertTrue(repo.deleteReviewedProfiles(latest, ids))
            val elapsed = android.os.SystemClock.elapsedRealtime() - start
            android.util.Log.i("RovunetDeleteQA", "rows=$count elapsed_ms=$elapsed")
            assertEquals(0, dao.getProfileCount())
        } finally { db.close() }
    }

    @Test fun lostLocalProxyPasswordIsExplicitAndNeverStartsAnUnauthenticatedProxy() = runBlocking<Unit> {
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val file = File(context.cacheDir, "proxy-qa-${UUID.randomUUID()}.preferences_pb")
        try {
            val store = PreferenceDataStoreFactory.create(scope = scope) { file }
            val prefs = PreferencesDataStore(context, store)
            val key = stringPreferencesKey("proxy_auth_password_sealed_v1")
            val foreign = foreignEnvelope("local-proxy-password-v1")
            prefs.setProxyAuthEnabled(true)
            store.edit { it[key] = foreign }
            assertTrue(prefs.proxyPasswordState.first().isFailure)
            assertFalse(prefs.proxyCredentialsReadable())
            assertTrue(runCatching { prefs.configuredSocksEndpoint() }.isFailure)
            assertEquals(foreign, store.data.first()[key])
            assertTrue(runCatching { prefs.setProxyAuthPassword("ordinary edit") }.isFailure)
            assertEquals(foreign, store.data.first()[key])
            assertFalse(prefs.replaceUnreadableProxyPassword(""))
            assertTrue(prefs.replaceUnreadableProxyPassword("replacement"))
            assertTrue(prefs.proxyCredentialsReadable())
            assertEquals("replacement", prefs.configuredSocksEndpoint().password)
            assertFalse(prefs.replaceUnreadableProxyPassword("stale"))
            assertNotEquals("replacement", store.data.first()[key])
        } finally { scope.cancel(); file.delete() }
    }
}
