package com.amirnourhan.zarpulse.data

import android.content.Context
import androidx.glance.appwidget.updateAll
import com.amirnourhan.zarpulse.widget.MarketWidget
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

class MarketRepository(private val context: Context) {
    private val store = MarketStore(context)

    suspend fun refresh(): MarketSnapshot = withContext(Dispatchers.IO) {
        var snapshot = store.load()
        var telegramOk = false
        var talaOk = false

        runCatching { TelegramPublicSource().fetch() }
            .onSuccess { t ->
                telegramOk = t.hasAnyQuote()
                snapshot = snapshot.copy(
                    abshodeh = t.abshodeh ?: snapshot.abshodeh,
                    gramTelegram = t.gram ?: snapshot.gramTelegram,
                    coinCash = t.coinCash ?: snapshot.coinCash,
                    coinHavaleh = t.coinHavaleh ?: snapshot.coinHavaleh
                )
            }

        val apiKey = store.talaApiKey
        if (apiKey.isNotBlank()) {
            runCatching { TalaApiSource().fetch(apiKey) }
                .onSuccess { t ->
                    talaOk = listOf(t.gold18, t.usd, t.eur).any { it?.value != null }
                    snapshot = snapshot.copy(
                        gold18 = t.gold18 ?: snapshot.gold18,
                        usd = t.usd ?: snapshot.usd,
                        eur = t.eur ?: snapshot.eur
                    )
                }
        }

        if (snapshot.gold18.value == null && snapshot.gramTelegram.value != null) {
            snapshot = snapshot.copy(
                gold18 = snapshot.gramTelegram.copy(label = "طلای ۱۸ عیار", source = "JUST")
            )
        }

        val anyFreshData = telegramOk || talaOk
        snapshot = snapshot.copy(
            updatedAt = if (anyFreshData) System.currentTimeMillis() else snapshot.updatedAt,
            telegramOk = telegramOk,
            talaOk = talaOk
        )

        store.save(snapshot)
        MarketWidget().updateAll(context)
        snapshot
    }
}
