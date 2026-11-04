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
    suspend fun deleteReviewedProfiles(expected: List<ProfileEntity>, deleteIds: List<Long>): Boolean {
        if (!deleteIds.all { id -> expected.any { it.id == id } }) return false
        for (profile in expected) if (getProfileById(profile.id) != profile) return false
        deleteIds.forEach { deleteProfile(it) }
        return true
    }

    @Query("DELETE FROM server_profiles WHERE id = :id")
    suspend fun deleteProfile(id: Long)

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

    @Query("UPDATE server_profiles SET is_pinned = NOT is_pinned WHERE id = :id")
    suspend fun togglePinned(id: Long)
}
