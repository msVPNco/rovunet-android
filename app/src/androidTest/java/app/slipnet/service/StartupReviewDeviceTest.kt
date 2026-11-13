package app.slipnet.service

import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.data.local.database.ProfileSnapshots
import app.slipnet.data.repository.ProfileRepositoryImpl
import app.slipnet.di.DatabaseModule
import app.slipnet.domain.model.ServerProfile
import com.google.gson.Gson
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Test

class StartupReviewDeviceTest {
    @Test fun deferredMapperDigestsKeepTheOriginalSnapshotAndRefuseStaleReviewedDeletion() = runBlocking<Unit> {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val db=DatabaseModule.provideDatabase(context)
        val mapper=ProfileMapper(Gson())
        val repo=ProfileRepositoryImpl(db.profileDao(),mapper)
        val id=repo.saveProfile(ServerProfile(name="Synthetic startup review",domain="192.0.2.12",sshPassword="synthetic"))
        try {
            val stored=requireNotNull(db.profileDao().getProfileById(id))
            val shown=mapper.toDomain(stored)
            val originalStoredDigest=ProfileSnapshots.digest(stored)
            val originalContentDigest=ProfileSnapshots.digest(mapper.toSnapshot(shown))
            // Change Room before first accessing the lazy review; it must still represent what was shown.
            repo.updateProfile(shown.copy(name="Edited after review"))
            assertEquals(originalStoredDigest,shown.deletionReview?.storedDigest)
            assertEquals(originalContentDigest,shown.deletionReview?.contentDigest)
            assertFalse(repo.deleteReviewedProfiles(listOf(shown),listOf(id)))
            assertNotNull(db.profileDao().getProfileById(id))
            val current=requireNotNull(repo.getProfileById(id))
            assertTrue(repo.deleteReviewedProfiles(listOf(current),listOf(id)))
            assertNull(db.profileDao().getProfileById(id))
        } finally { db.profileDao().deleteProfile(id);db.close() }
    }
}
