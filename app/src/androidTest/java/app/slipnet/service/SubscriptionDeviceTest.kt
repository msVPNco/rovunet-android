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
import org.junit.Assert.*
import org.junit.Test

class SubscriptionDeviceTest {
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
