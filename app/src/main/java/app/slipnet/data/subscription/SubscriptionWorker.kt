package app.slipnet.data.subscription

import android.content.Context
import androidx.work.*
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.components.SingletonComponent
import java.util.concurrent.TimeUnit

@EntryPoint
@InstallIn(SingletonComponent::class)
interface SubscriptionWorkerEntryPoint { fun subscriptions(): SubscriptionRepository }

class SubscriptionWorker(context: Context, parameters: WorkerParameters) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result {
        EntryPointAccessors.fromApplication(applicationContext, SubscriptionWorkerEntryPoint::class.java)
            .subscriptions().refreshAutomatic()
        return Result.success()
    }
    companion object {
        fun schedule(context: Context) {
            val request = PeriodicWorkRequestBuilder<SubscriptionWorker>(12, TimeUnit.HOURS)
                .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.UNMETERED).setRequiresBatteryNotLow(true).build())
                .setInitialDelay(12, TimeUnit.HOURS).build()
            WorkManager.getInstance(context).enqueueUniquePeriodicWork("rovunet-subscriptions", ExistingPeriodicWorkPolicy.KEEP, request)
        }
    }
}
