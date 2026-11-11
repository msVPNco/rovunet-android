package app.slipnet.data.repository

import app.slipnet.data.local.database.CollectionMembership
import app.slipnet.data.local.database.ProfileDao
import app.slipnet.data.local.database.ProfileSnapshots
import app.slipnet.data.local.database.ReviewedProfileRow
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.repository.ProfileRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.coroutines.flow.flowOn
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class ProfileRepositoryImpl @Inject constructor(
    private val profileDao: ProfileDao,
    private val profileMapper: ProfileMapper
) : ProfileRepository {

    override fun getAllProfiles(): Flow<List<ServerProfile>> {
        return profileDao.getAllProfiles().map { entities ->
            profileMapper.toDomainList(entities)
        }.flowOn(Dispatchers.IO)
    }

    override fun getActiveProfile(): Flow<ServerProfile?> {
        return profileDao.getActiveProfile().map { entity ->
            entity?.let { profileMapper.toDomain(it) }
        }.flowOn(Dispatchers.IO)
    }

    override suspend fun getProfileById(id: Long): ServerProfile? = withContext(Dispatchers.IO) {
        profileDao.getProfileById(id)?.let { profileMapper.toDomain(it) }
    }

    override suspend fun saveProfile(profile: ServerProfile): Long = withContext(Dispatchers.IO) {
        check(!profile.needsCredentials) { "Saved credentials unavailable" }
        profileDao.saveReadable(profileMapper.toSnapshot(profile))
    }

    override suspend fun updateProfiles(profiles: List<ServerProfile>) = withContext(Dispatchers.IO) {
        check(profiles.none { it.needsCredentials }) { "Saved credentials unavailable" }
        profileDao.updateReadable(profiles.map(profileMapper::toSnapshot))
    }

    override suspend fun updateCollectionMemberships(profiles: List<ServerProfile>) = withContext(Dispatchers.IO) {
        profileDao.updateMemberships(profiles.map { CollectionMembership(it.id, it.collectionId, it.collectionName) })
    }

    override suspend fun restoreCredentials(reviewed: ServerProfile, replacement: ServerProfile): Boolean = withContext(Dispatchers.IO) {
        val token = reviewed.unreadableEnvelopeToken ?: return@withContext false
        if (replacement.needsCredentials) return@withContext false
        profileDao.restoreCredentials(reviewed.id, token, profileMapper.toSnapshot(replacement))
    }

    override suspend fun insertCollection(profiles: List<ServerProfile>): List<Long> = withContext(Dispatchers.IO) {
        profileDao.insertCollection(profiles.map { profileMapper.toEntity(it) })
    }

    override suspend fun updateProfile(profile: ServerProfile) = withContext(Dispatchers.IO) {
        updateProfiles(listOf(profile))
    }

    override suspend fun deleteReviewedProfiles(expected: List<ServerProfile>, deleteIds: List<Long>): Boolean =
        withContext(Dispatchers.IO) {
            val reviewed = expected.map { profile ->
                val review = profile.deletionReview ?: return@withContext false
                if (review.contentDigest != ProfileSnapshots.digest(profileMapper.toSnapshot(profile))) return@withContext false
                ReviewedProfileRow(profile.id, review.storedDigest)
            }
            profileDao.deleteReviewedProfiles(reviewed, deleteIds)
        }

    override suspend fun deleteProfile(id: Long) {
        profileDao.deleteProfile(id)
    }

    override suspend fun setActiveProfile(id: Long) {
        profileDao.activateProfile(id)
    }

    override suspend fun clearActiveProfile() {
        profileDao.clearActiveProfile()
    }

    override suspend fun updateLastConnectedAt(id: Long) {
        profileDao.updateLastConnectedAt(id, System.currentTimeMillis())
    }

    override suspend fun updateProfileOrder(orderedIds: List<Long>) = withContext(Dispatchers.IO) {
        profileDao.updateSortOrders(orderedIds)
    }

    override suspend fun getMaxSortOrder(): Int {
        return profileDao.getMaxSortOrder() ?: -1
    }

    override suspend fun prepareTopSortOrder() {
        profileDao.incrementAllSortOrders()
    }

    override suspend fun togglePinned(id: Long) {
        profileDao.togglePinned(id)
    }
}
