package app.slipnet.service

import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.export.*
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.data.subscription.SubscriptionWorkerEntryPoint
import app.slipnet.di.DatabaseModule
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import com.google.gson.Gson
import dagger.hilt.android.EntryPointAccessors
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.sync.withLock
import org.junit.Assert.*
import org.junit.Test

class SubscriptionDeviceTest {
    @Test fun sharedWebsocketHostsStayDistinctAndLegacyRefreshKeepsLocalRows() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val repository = EntryPointAccessors.fromApplication(context, SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db = DatabaseModule.provideDatabase(context)
        val mapper = ProfileMapper(Gson())
        val address = "https://example.invalid/shared-host-fixture"
        val uuid = "00000000-0000-4000-8000-000000000001"
        val first = "vless://$uuid@192.0.2.31:443?type=ws&security=tls&host=cdn.example.invalid&path=%2Ffixture&sni=tls.example.invalid#First"
        val second = first.replace("192.0.2.31:443", "192.0.2.32:8443").replace("#First", "#Second")
        val initial = repository.prepareDocument("Shared host fixture", address, null, false, first)
        val sourceId = initial.source.id
        try {
            repository.apply(initial)
            val row = db.profileDao().getAllProfiles().first().single { it.collectionId == sourceId }
            val remote = mapper.toDomain(row)
            val identity = app.slipnet.data.subscription.SubscriptionIdentity.legacy(remote)
            val legacyKey = java.security.MessageDigest.getInstance("SHA-256").digest(identity.toByteArray())
                .joinToString("") { "%02x".format(it) }
            // Emulate the membership key stored by the already-shipped updater.
            val saved = requireNotNull(db.subscriptionDao().get(sourceId))
            val members = org.json.JSONArray(saved.membersJson)
            members.getJSONObject(0).put("key", legacyKey)
            db.subscriptionDao().put(saved.copy(membersJson = members.toString()))
            db.profileDao().updateProfile(mapper.toEntity(remote.copy(name = "Local override")))
            val edited = requireNotNull(db.profileDao().getProfileById(row.id))
            val update = repository.prepareDocument(initial.source.name, address, sourceId, false, first + "\n" + second)
            assertEquals(2, update.profiles.size)
            assertEquals(0, update.removed)
            assertEquals(listOf(legacyKey), update.legacyKeys.values.toList())
            repository.apply(update)
            val current = db.profileDao().getAllProfiles().first().filter { it.collectionId == sourceId }
            assertEquals(2, current.size)
            assertEquals("Local override", db.profileDao().getProfileById(row.id)?.name)
            assertEquals(edited.secretEnvelope, db.profileDao().getProfileById(row.id)?.secretEnvelope)
            val again = repository.prepareDocument(initial.source.name, address, sourceId, false, first + "\n" + second)
            assertTrue(again.legacyKeys.isEmpty()); assertEquals(0, again.removed)
            repository.apply(again)
            assertEquals(current.map { it.id }.toSet(), db.profileDao().getAllProfiles().first().filter { it.collectionId == sourceId }.map { it.id }.toSet())
            assertTrue(runCatching { repository.prepareDocument(initial.source.name, address, sourceId, false,
                first + "\n" + second + "\n" + "vless://$uuid@example.invalid:443?type=xhttp&security=none&encryption=unknown") }.isFailure)
            assertEquals(2, db.profileDao().getAllProfiles().first().count { it.collectionId == sourceId })
        } finally {
            repository.detach(sourceId)
            db.profileDao().getAllProfiles().first().filter { it.collectionId == sourceId || it.collectionId == sourceId + ":retained" }.forEach { db.profileDao().deleteProfile(it.id) }
            db.close()
        }
    }
    @Test fun newSourcePreservesTunnelOwnershipAndDuplicateImportsAreAtomic() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val repository = EntryPointAccessors.fromApplication(context, SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db = DatabaseModule.provideDatabase(context)
        val remote = ServerProfile(name = "New source fixture", tunnelType = TunnelType.SSH, domain = "192.0.2.52",
            sshHost = "192.0.2.52", sshUsername = "fixture", sshPassword = "synthetic")
        val document = ConfigExporter().exportSingleProfile(remote)
        val address = "https://example.invalid/subscription-add-with-owner"
        val preview = repository.prepareDocument("New source fixture", address, null, false, document)
        TunnelOperations.mutex.withLock {
            assertEquals(0L, TunnelOperations.owner)
            TunnelOperations.owner = 717171L
        }
        try {
            // A new source must also finish while native ownership is locked elsewhere.
            kotlinx.coroutines.withTimeout(5000) { TunnelOperations.mutex.withLock { repository.apply(preview) } }
            assertEquals(717171L, TunnelOperations.owner)
            val saved = db.profileDao().getAllProfiles().first().single { it.collectionId == preview.source.id }
            assertFalse(saved.isActive)
            val repeated = repository.prepareDocument("Duplicate fixture", address, null, false, document)
            assertEquals("SOURCE_EXISTS", runCatching { repository.apply(repeated) }.exceptionOrNull()?.message)
            assertNull(db.subscriptionDao().get(repeated.source.id))
            val update = repository.prepareDocument("Updated fixture", address, preview.source.id, false, document)
            assertEquals("DISCONNECT_TO_UPDATE", runCatching { repository.apply(update) }.exceptionOrNull()?.message)
            assertEquals(saved.id, db.profileDao().getAllProfiles().first().single { it.collectionId == preview.source.id }.id)
            assertEquals(717171L, TunnelOperations.owner)
        } finally {
            TunnelOperations.mutex.withLock { TunnelOperations.owner = 0 }
            repository.detach(preview.source.id)
            db.profileDao().getAllProfiles().first().filter { it.collectionId == preview.source.id }.forEach { db.profileDao().deleteProfile(it.id) }
            db.close()
        }
    }
    @Test fun updatesAreAtomicPreserveIdentityAndDoNotOverwriteLocalChanges() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val repository = EntryPointAccessors.fromApplication(context, SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db = DatabaseModule.provideDatabase(context)
        val mapper = ProfileMapper(Gson())
        val exporter = ConfigExporter()
        val remote = ServerProfile(name = "Subscription fixture", tunnelType = TunnelType.SSH, domain = "192.0.2.40",
            sshHost = "192.0.2.40", sshUsername = "fixture", sshPassword = "first|password")
        val preview = repository.prepareDocument("Subscription fixture", "https://example.invalid/sub", null, false, exporter.exportSingleProfile(remote))
        val sourceId = preview.source.id
        try {
            repository.apply(preview)
            val row = db.profileDao().getAllProfiles().first().single { it.collectionId == sourceId }
            assertEquals(remote.sshPassword, mapper.toDomain(row).sshPassword)
            assertFalse(row.secretEnvelope.contains("first|password"))
            assertFalse(db.subscriptionDao().get(sourceId)?.sourceEnvelope.orEmpty().contains("example.invalid"))
            repository.apply(repository.prepareDocument(preview.source.name, "https://example.invalid/sub", sourceId, false, exporter.exportSingleProfile(remote)))
            assertEquals("A no-op update must not reseal credentials", row.secretEnvelope, db.profileDao().getProfileById(row.id)?.secretEnvelope)
            val refreshed = repository.prepareDocument(preview.source.name, "https://example.invalid/sub", sourceId, false,
                exporter.exportSingleProfile(remote.copy(sshPassword = "second")))
            repository.apply(refreshed)
            assertEquals("second", mapper.toDomain(requireNotNull(db.profileDao().getProfileById(row.id))).sshPassword)
            assertEquals(row.id, db.profileDao().getAllProfiles().first().single { it.collectionId == sourceId }.id)
            val local = mapper.toDomain(requireNotNull(db.profileDao().getProfileById(row.id))).copy(name = "Local override")
            db.profileDao().updateProfile(mapper.toEntity(local))
            repository.apply(repository.prepareDocument(preview.source.name, "https://example.invalid/sub", sourceId, false,
                exporter.exportSingleProfile(remote.copy(sshPassword = "third"))))
            assertEquals("Local override", mapper.toDomain(requireNotNull(db.profileDao().getProfileById(row.id))).name)
            assertEquals("second", mapper.toDomain(requireNotNull(db.profileDao().getProfileById(row.id))).sshPassword)
            assertTrue(runCatching { repository.prepareDocument(preview.source.name, "https://example.invalid/sub", sourceId, false, "<html>error</html>") }.isFailure)
            assertNotNull(db.profileDao().getProfileById(row.id))
            assertTrue(runCatching { repository.apply(refreshed) }.isFailure)
            val changed = remote.copy(domain = "192.0.2.41", sshHost = "192.0.2.41")
            val removal = repository.prepareDocument(preview.source.name, "https://example.invalid/sub", sourceId, false, exporter.exportSingleProfile(changed))
            assertEquals(1, removal.removed)
            repository.apply(removal)
            assertEquals(sourceId + ":retained", db.profileDao().getProfileById(row.id)?.collectionId)
            val currentRow = db.profileDao().getAllProfiles().first().single { it.collectionId == sourceId }
            db.profileDao().updateMembership(currentRow.id, sourceId, "Local collection title")
            repository.apply(repository.prepareDocument(preview.source.name, "https://example.invalid/sub", sourceId, false, exporter.exportSingleProfile(changed)))
            assertEquals("Local collection title", db.profileDao().getProfileById(currentRow.id)?.collectionName)
            assertEquals("Local collection title", db.subscriptionDao().get(sourceId)?.name)
            repository.detach(sourceId)
            assertNotNull(db.profileDao().getProfileById(row.id))
        } finally {
            repository.detach(sourceId)
            db.profileDao().getAllProfiles().first().filter { it.collectionId == sourceId || it.collectionId == sourceId + ":retained" }.forEach { db.profileDao().deleteProfile(it.id) }
            db.close()
        }
    }
    @Test fun collectionQrRoundTripPreservesEveryProfileAndSharingRestrictions() {
        val exporter = ConfigExporter()
        val first = ServerProfile(name = "QR one", tunnelType = TunnelType.SSH, domain = "192.0.2.1",
            sshHost = "192.0.2.1", sshUsername = "fixture", sshPassword = "p|one", collectionId = "qr-fixture", collectionName = "QR fixture")
        val second = first.copy(name = "QR two", sshPort = 2222, sshPassword = "p|two")
        val payload = CollectionLink.encode(exporter.exportCollection(listOf(first, second), first))
        val bitmap = requireNotNull(app.slipnet.presentation.common.components.generateQrCodeBitmap(payload))
        val pixels = IntArray(bitmap.width * bitmap.height)
        bitmap.getPixels(pixels, 0, bitmap.width, 0, 0, bitmap.width, bitmap.height)
        val image = com.google.zxing.BinaryBitmap(com.google.zxing.common.HybridBinarizer(
            com.google.zxing.RGBLuminanceSource(bitmap.width, bitmap.height, pixels)))
        val decoded = com.google.zxing.MultiFormatReader().decode(image).text
        assertEquals(payload, decoded)
        val imported = ConfigImporter().parseAndImport(decoded) as ImportResult.Success
        assertEquals(listOf("p|one", "p|two"), imported.profiles.map { it.sshPassword })
        assertTrue(runCatching { exporter.exportCollection(listOf(first, second.copy(isLocked = true, allowSharing = false)), first) }.isFailure)
        bitmap.recycle()
    }
}
