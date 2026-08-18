package com.amirnourhan.zarpulse.data

import org.junit.Assert.assertEquals
import org.junit.Test

class TelegramParserTest {
    @Test
    fun parsesLatestJustQuotes() {
        val text = """
            🔻#ابشـده‌حواله 82,580,000
            🔻#گرم‌طلا: 19,064,108
            #سکه‌نقدی: 🔻 187,200,000 🔻
            #سکه‌حواله: 🔺 190,400,000 🔺
            🔺#ابشـده‌حواله 82,600,000
            🔺#گرم‌طلا: 19,068,725
        """.trimIndent()

        val parsed = TelegramParser.parse(text)
        assertEquals(82_600_000L, parsed.abshodeh?.value)
        assertEquals(19_068_725L, parsed.gram?.value)
        assertEquals(187_200_000L, parsed.coinCash?.value)
        assertEquals(-1, parsed.coinCash?.direction)
        assertEquals(190_400_000L, parsed.coinHavaleh?.value)
        assertEquals(1, parsed.coinHavaleh?.direction)
    }

    @Test
    fun parsesPersianDigits() {
        val text = "🔺#ابشـده‌حواله ۸۲٬۶۰۰٬۰۰۰"
        val parsed = TelegramParser.parse(text)
        assertEquals(82_600_000L, parsed.abshodeh?.value)
    }
}
