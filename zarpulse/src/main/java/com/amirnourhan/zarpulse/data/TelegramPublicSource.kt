package com.amirnourhan.zarpulse.data

import android.text.Html
import java.net.HttpURLConnection
import java.net.URL

class TelegramPublicSource {
    fun fetch(): TelegramParser.Parsed {
        val connection = (URL("https://t.me/s/abshdh").openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            connectTimeout = 12_000
            readTimeout = 12_000
            setRequestProperty("User-Agent", "Mozilla/5.0 (Android) ZarPulse/1.0")
            setRequestProperty("Accept-Language", "fa,en;q=0.8")
        }

        try {
            val code = connection.responseCode
            if (code !in 200..299) error("Telegram HTTP $code")
            val html = connection.inputStream.bufferedReader(Charsets.UTF_8).use { it.readText() }
            val plain = Html.fromHtml(html, Html.FROM_HTML_MODE_LEGACY).toString()
            return TelegramParser.parse(plain)
        } finally {
            connection.disconnect()
        }
    }
}
