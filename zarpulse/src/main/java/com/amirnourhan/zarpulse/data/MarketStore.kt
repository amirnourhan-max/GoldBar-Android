package com.amirnourhan.zarpulse.data

import android.content.Context

class MarketStore(context: Context) {
    private val prefs = context.getSharedPreferences("market_cache", Context.MODE_PRIVATE)
    private val settings = context.getSharedPreferences("settings", Context.MODE_PRIVATE)

    var talaApiKey: String
        get() = settings.getString("tala_api_key", "").orEmpty()
        set(value) { settings.edit().putString("tala_api_key", value.trim()).apply() }

    fun save(snapshot: MarketSnapshot) {
        prefs.edit().apply {
            putQuote("abshodeh", snapshot.abshodeh)
            putQuote("gramTelegram", snapshot.gramTelegram)
            putQuote("coinCash", snapshot.coinCash)
            putQuote("coinHavaleh", snapshot.coinHavaleh)
            putQuote("gold18", snapshot.gold18)
            putQuote("usd", snapshot.usd)
            putQuote("eur", snapshot.eur)
            putLong("updatedAt", snapshot.updatedAt)
            putBoolean("telegramOk", snapshot.telegramOk)
            putBoolean("talaOk", snapshot.talaOk)
        }.apply()
    }

    fun load(): MarketSnapshot = MarketSnapshot(
        abshodeh = getQuote("abshodeh", "آبشده حواله"),
        gramTelegram = getQuote("gramTelegram", "گرم طلا"),
        coinCash = getQuote("coinCash", "سکه نقدی"),
        coinHavaleh = getQuote("coinHavaleh", "سکه حواله"),
        gold18 = getQuote("gold18", "طلای ۱۸ عیار"),
        usd = getQuote("usd", "دلار"),
        eur = getQuote("eur", "یورو"),
        updatedAt = prefs.getLong("updatedAt", 0L),
        telegramOk = prefs.getBoolean("telegramOk", false),
        talaOk = prefs.getBoolean("talaOk", false)
    )

    private fun android.content.SharedPreferences.Editor.putQuote(prefix: String, q: Quote) {
        q.value?.let { putLong("${prefix}_value", it) } ?: remove("${prefix}_value")
        putString("${prefix}_unit", q.unit)
        putInt("${prefix}_direction", q.direction)
        putString("${prefix}_source", q.source)
    }

    private fun getQuote(prefix: String, label: String): Quote {
        val has = prefs.contains("${prefix}_value")
        return Quote(
            label = label,
            value = if (has) prefs.getLong("${prefix}_value", 0L) else null,
            unit = prefs.getString("${prefix}_unit", "تومان").orEmpty(),
            direction = prefs.getInt("${prefix}_direction", 0),
            source = prefs.getString("${prefix}_source", "").orEmpty()
        )
    }
}
