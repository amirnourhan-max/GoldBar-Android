package com.amirnourhan.zarpulse.data

import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import java.util.concurrent.TimeUnit

class MarketWorker(
    appContext: Context,
    params: WorkerParameters
) : CoroutineWorker(appContext, params) {
    override suspend fun doWork(): Result = runCatching {
        MarketRepository(applicationContext).refresh()
        Result.success()
    }.getOrElse { Result.retry() }

    companion object {
        private const val PERIODIC_WORK = "zar_pulse_market_refresh"
        private const val NOW_WORK = "zar_pulse_market_refresh_now"

        private fun networkConstraint() = Constraints.Builder()
            .setRequiredNetworkType(NetworkType.CONNECTED)
            .build()

        fun schedule(context: Context) {
            val request = PeriodicWorkRequestBuilder<MarketWorker>(15, TimeUnit.MINUTES)
                .setConstraints(networkConstraint())
                .build()

            WorkManager.getInstance(context).enqueueUniquePeriodicWork(
                PERIODIC_WORK,
                ExistingPeriodicWorkPolicy.UPDATE,
                request
            )
        }

        fun refreshNow(context: Context) {
            val request = OneTimeWorkRequestBuilder<MarketWorker>()
                .setConstraints(networkConstraint())
                .build()

            WorkManager.getInstance(context).enqueueUniqueWork(
                NOW_WORK,
                ExistingWorkPolicy.REPLACE,
                request
            )
        }
    }
}
