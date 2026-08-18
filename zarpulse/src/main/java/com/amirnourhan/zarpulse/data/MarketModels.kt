package com.amirnourhan.zarpulse.data

data class Quote(
    val label: String,
    val value: Long? = null,
    val unit: String = "تومان",
    val direction: Int = 0,
    val source: String = ""
)

data class MarketSnapshot(
    val abshodeh: Quote = Quote("آبشده حواله"),
    val gramTelegram: Quote = Quote("گرم طلا"),
    val coinCash: Quote = Quote("سکه نقدی"),
    val coinHavaleh: Quote = Quote("سکه حواله"),
    val gold18: Quote = Quote("طلای ۱۸ عیار"),
    val usd: Quote = Quote("دلار"),
    val eur: Quote = Quote("یورو"),
    val updatedAt: Long = 0L,
    val telegramOk: Boolean = false,
    val talaOk: Boolean = false
)
