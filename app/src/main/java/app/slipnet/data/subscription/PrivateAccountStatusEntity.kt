package app.slipnet.data.subscription

import androidx.room.*
import kotlinx.coroutines.flow.Flow

/** Kept per source fingerprint, including after URL edits, until the source is explicitly detached. */
@Entity(tableName = "private_account_status", primaryKeys = ["subscriptionId", "sourceFingerprint"],
    foreignKeys = [ForeignKey(entity = SubscriptionEntity::class, parentColumns = ["id"],
        childColumns = ["subscriptionId"], onDelete = ForeignKey.CASCADE)])
data class PrivateAccountStatusEntity(val subscriptionId: String, val sourceFingerprint: String,
    val serverId: String, val accountId: String, val highWater: Long, val payload: String,
    val checkedAt: Long, val checkedElapsed: Long, val serverTime: Long, val error: String) {
    internal fun binding() = PrivateStatusBinding(sourceFingerprint, serverId, accountId, highWater)
    fun display() = AccountStatusEntity(subscriptionId, sourceFingerprint, payload, checkedAt, checkedElapsed, serverTime, error)
}

@Dao
interface PrivateAccountStatusDao {
    @Query("SELECT * FROM private_account_status") fun observe(): Flow<List<PrivateAccountStatusEntity>>
    @Query("SELECT * FROM private_account_status WHERE subscriptionId = :id AND sourceFingerprint = :fingerprint")
    suspend fun get(id: String, fingerprint: String): PrivateAccountStatusEntity?
    @Query("SELECT EXISTS(SELECT 1 FROM private_account_status WHERE subscriptionId = :id)")
    suspend fun hasHistory(id: String): Boolean
    @Insert(onConflict = OnConflictStrategy.REPLACE) suspend fun put(row: PrivateAccountStatusEntity)
}
