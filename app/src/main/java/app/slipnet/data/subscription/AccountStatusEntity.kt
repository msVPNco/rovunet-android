package app.slipnet.data.subscription

import androidx.room.*
import kotlinx.coroutines.flow.Flow

@Entity(tableName = "account_status", foreignKeys = [ForeignKey(entity = SubscriptionEntity::class,
    parentColumns = ["id"], childColumns = ["subscriptionId"], onDelete = ForeignKey.CASCADE)])
data class AccountStatusEntity(@PrimaryKey val subscriptionId: String, val sourceFingerprint: String,
    val payload: String, val checkedAt: Long, val checkedElapsed: Long, val serverTime: Long, val error: String) {
    fun view(now: Long = System.currentTimeMillis(), elapsed: Long = android.os.SystemClock.elapsedRealtime()): AccountStatusView =
        AccountStatusView.evaluate(runCatching { SubscriptionMetadata.readStatus(payload) }.getOrNull(), checkedAt, checkedElapsed, serverTime, error, now, elapsed)
}

@Dao
interface AccountStatusDao {
    @Query("SELECT * FROM account_status") fun observe(): Flow<List<AccountStatusEntity>>
    @Query("SELECT * FROM account_status WHERE subscriptionId = :id") suspend fun get(id: String): AccountStatusEntity?
    @Insert(onConflict = OnConflictStrategy.REPLACE) suspend fun put(row: AccountStatusEntity)
}
