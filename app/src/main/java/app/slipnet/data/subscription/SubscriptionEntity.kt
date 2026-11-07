package app.slipnet.data.subscription

import androidx.room.*
import kotlinx.coroutines.flow.Flow

@Entity(tableName = "subscriptions")
data class SubscriptionEntity(
    @PrimaryKey val id: String,
    val name: String,
    val sourceEnvelope: String,
    val membersJson: String = "[]",
    val lastSuccess: Long = 0,
    val lastError: String = "",
    val autoUpdate: Boolean = false
)

@Dao
interface SubscriptionDao {
    @Query("SELECT * FROM subscriptions ORDER BY name") fun observe(): Flow<List<SubscriptionEntity>>
    @Query("SELECT * FROM subscriptions ORDER BY name") suspend fun all(): List<SubscriptionEntity>
    @Query("SELECT * FROM subscriptions WHERE id = :id") suspend fun get(id: String): SubscriptionEntity?
    @Insert(onConflict = OnConflictStrategy.REPLACE) suspend fun put(row: SubscriptionEntity)
    @Query("DELETE FROM subscriptions WHERE id = :id") suspend fun delete(id: String)
}
