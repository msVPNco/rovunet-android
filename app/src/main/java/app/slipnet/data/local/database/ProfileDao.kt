package app.slipnet.data.local.database

import androidx.room.Transaction
import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Update
import kotlinx.coroutines.flow.Flow

@Dao
interface ProfileDao {
    @Query("SELECT * FROM server_profiles ORDER BY is_pinned DESC, sort_order ASC")
    fun getAllProfiles(): Flow<List<ProfileEntity>>

    @Query("SELECT * FROM server_profiles WHERE is_active = 1 LIMIT 1")
    fun getActiveProfile(): Flow<ProfileEntity?>

    @Query("SELECT * FROM server_profiles WHERE id = :id")
    suspend fun getProfileById(id: Long): ProfileEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertProfile(profile: ProfileEntity): Long

    @Update
    suspend fun updateProfile(profile: ProfileEntity)

    @Update
    suspend fun updateProfiles(profiles: List<ProfileEntity>)

    @Query("UPDATE server_profiles SET sort_order = sort_order + :count")
    suspend fun shiftSortOrders(count: Int)

    @Transaction
    suspend fun insertCollection(profiles: List<ProfileEntity>): List<Long> {
        shiftSortOrders(profiles.size)
        return profiles.mapIndexed { index, profile -> insertProfile(profile.copy(id = 0, sortOrder = index, isActive = false)) }
    }

    @Transaction
    suspend fun saveReadable(snapshot: ProfileEntity): Long {
        if (snapshot.id != 0L) getProfileById(snapshot.id)?.let {
            check(ProfileSecrets.open(it).isSuccess) { "Saved credentials unavailable" }
        }
        return insertProfile(ProfileSecrets.seal(snapshot))
    }

    @Transaction
    suspend fun updateReadable(snapshots: List<ProfileEntity>) {
        snapshots.forEach { snapshot ->
            val current = checkNotNull(getProfileById(snapshot.id)) { "Profile no longer exists" }
            check(ProfileSecrets.open(current).isSuccess) { "Saved credentials unavailable" }
            updateProfile(ProfileSecrets.seal(snapshot))
        }
    }

    @Query("UPDATE server_profiles SET collectionId = :collectionId, collectionName = :name WHERE id = :id")
    suspend fun updateMembership(id: Long, collectionId: String, name: String): Int

    @Transaction
    suspend fun updateMemberships(changes: List<CollectionMembership>) {
        changes.forEach { check(updateMembership(it.id, it.collectionId, it.name) == 1) { "Profile no longer exists" } }
    }

    /** Replace only secrets, and only in the still-unreadable row the user explicitly reviewed. */
    @Transaction
    suspend fun restoreCredentials(id: Long, expectedToken: String, source: ProfileEntity): Boolean {
        val row = getProfileById(id) ?: return false
        if (row.isLocked || source.isLocked || row.secretEnvelope.isEmpty() ||
            ProfileSecrets.token(row) != expectedToken || ProfileSecrets.open(row).isSuccess) return false
        if (row.tunnelType != source.tunnelType || !row.domain.equals(source.domain, ignoreCase = true) ||
            row.sshHost != source.sshHost || row.sshPort != source.sshPort ||
            row.naivePort != source.naivePort || row.socks5ServerPort != source.socks5ServerPort ||
            row.cdnIp != source.cdnIp || row.cdnPort != source.cdnPort ||
            row.sshUsername != source.sshUsername || row.socksUsername != source.socksUsername ||
            row.naiveUsername != source.naiveUsername || row.sshAuthType != source.sshAuthType ||
            source.expirationDate != row.expirationDate || source.boundDeviceId != row.boundDeviceId ||
            source.allowSharing != row.allowSharing || source.resolversHidden != row.resolversHidden ||
            source.lockPasswordHash.isNotEmpty()) return false
        val restored = ProfileSecrets.seal(row.copy(
            sshPassword = source.sshPassword, sshPrivateKey = source.sshPrivateKey,
            sshKeyPassphrase = source.sshKeyPassphrase, socksPassword = source.socksPassword,
            naivePassword = source.naivePassword, coreLink = source.coreLink, vlessUuid = source.vlessUuid,
            lockPasswordHash = "", updatedAt = System.currentTimeMillis()
        ))
        if (restored.secretEnvelope.isEmpty()) return false
        ProfileSecrets.open(restored).getOrThrow()
        updateProfile(restored)
        return true
    }

    @Query("SELECT * FROM server_profiles WHERE id IN (:ids)")
    suspend fun getProfilesByIds(ids: List<Long>): List<ProfileEntity>

    @Query("DELETE FROM server_profiles WHERE id IN (:ids)")
    suspend fun deleteProfileRows(ids: List<Long>): Int

    /** Every reviewed row must match, before any delete. Hashing the full stored row avoids N Keystore decryptions. */
    @Transaction
    suspend fun deleteReviewedProfiles(expected: List<ReviewedProfileRow>, deleteIds: List<Long>): Boolean {
        val reviewed = expected.associate { it.id to it.digest }
        if (reviewed.size != expected.size || deleteIds.isEmpty() || deleteIds.distinct().size != deleteIds.size ||
            !reviewed.keys.containsAll(deleteIds)) return false
        for (ids in reviewed.keys.chunked(400)) {
            val rows = getProfilesByIds(ids)
            if (rows.size != ids.size || rows.any { ProfileSnapshots.digest(it) != reviewed[it.id] }) return false
        }
        val deleting = deleteIds.toSet()
        check(chainSnapshots().none { chain ->
            val ids = org.json.JSONArray(chain.profileIdsJson)
            (0 until ids.length()).any { ids.getLong(it) in deleting }
        }) { "Remove this profile from its chain first" }
        for (ids in deleteIds.chunked(400)) check(deleteProfileRows(ids) == ids.size)
        return true
    }

    @Query("SELECT * FROM profile_chains")
    suspend fun chainSnapshots(): List<ChainEntity>

    @Transaction
    suspend fun deleteProfile(id: Long) {
        check(chainSnapshots().none { chain ->
            val ids = org.json.JSONArray(chain.profileIdsJson)
            (0 until ids.length()).any { ids.getLong(it) == id }
        }) { "Remove this profile from its chain first" }
        deleteProfileRow(id)
    }

    @Query("DELETE FROM server_profiles WHERE id = :id")
    suspend fun deleteProfileRow(id: Long)

    @Transaction
    suspend fun activateProfile(id: Long) {
        clearActiveProfile()
        setActiveProfile(id)
    }

    @Query("UPDATE server_profiles SET is_active = 0")
    suspend fun clearActiveProfile()

    @Query("UPDATE server_profiles SET is_active = 1 WHERE id = :id")
    suspend fun setActiveProfile(id: Long)

    @Query("SELECT COUNT(*) FROM server_profiles")
    suspend fun getProfileCount(): Int

    @Query("UPDATE server_profiles SET last_connected_at = :timestamp WHERE id = :id")
    suspend fun updateLastConnectedAt(id: Long, timestamp: Long)

    @Query("SELECT MAX(sort_order) FROM server_profiles")
    suspend fun getMaxSortOrder(): Int?

    @Query("SELECT MIN(sort_order) FROM server_profiles")
    suspend fun getMinSortOrder(): Int?

    @Query("UPDATE server_profiles SET sort_order = sort_order + 1")
    suspend fun incrementAllSortOrders()

    @Query("UPDATE server_profiles SET sort_order = :sortOrder WHERE id = :id")
    suspend fun updateSortOrder(id: Long, sortOrder: Int)

    @Transaction
    suspend fun updateSortOrders(orderedIds: List<Long>) {
        orderedIds.forEachIndexed { index, id -> updateSortOrder(id, index) }
    }

    @Query("UPDATE server_profiles SET is_pinned = NOT is_pinned WHERE id = :id")
    suspend fun togglePinned(id: Long)
}

data class CollectionMembership(val id: Long, val collectionId: String, val name: String)
