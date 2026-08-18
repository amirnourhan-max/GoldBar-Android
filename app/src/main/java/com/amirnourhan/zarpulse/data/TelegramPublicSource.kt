package com.amirnourhan.zarpulse.data

import android.text.Html
import java.net.HttpURLConnection
import java.net.URL

class TelegramPublicSource {
    private val endpoints = listOf(
        "https://t.me/s/abshdh",
        "https://telegram.me/s/abshdh"
    )

    fun fetch(): TelegramParser.Parsed {
        var lastError: Throwable? = null
        endpoints.forEach { endpoint ->
            runCatching { fetchFrom(endpoint) }
                .onSuccess { parsed -> if (parsed.hasAnyQuote()) return parsed }
                .onFailure { lastError = it }
        }
        throw IllegalStateException("JUST data unavailable", lastError)
    }

    private fun fetchFrom(endpoint: String): TelegramParser.Parsed {
        val connection = (URL(endpoint).openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            instanceFollowRedirects = true
            connectTimeout = 15_000
            readTimeout = 15_000
            useCaches = false
            setRequestProperty("User-Agent", "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/131 Mobile Safari/537.36")
            setRequestProperty("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")
            setRequestProperty("Accept-Language", "fa-IR,fa;q=0.9,en;q=0.7")
            setRequestProperty("Cache-Control", "no-cache")
            setRequestProperty("Pragma", "no-cache")
        }

        try {
            val code = connection.responseCode
            if (code !in 200..299) error("Telegram HTTP $code")

            val html = connection.inputStream.bufferedReader(Charsets.UTF_8).use { it.readText() }
            if (html.length < 500) error("Telegram returned an empty page")

            val messageBlocks = Regex(
                "<div[^>]+class=\"[^\"]*tgme_widget_message_text[^\"]*\"[^>]*>(.*?)</div>",
                setOf(RegexOption.IGNORE_CASE, RegexOption.DOT_MATCHES_ALL)
            ).findAll(html).map { it.groupValues[1] }.toList()

            val sourceHtml = if (messageBlocks.isNotEmpty()) messageBlocks.joinToString("<br>") else html
            val plain = Html.fromHtml(sourceHtml, Html.FROM_HTML_MODE_LEGACY).toString()
            val parsed = TelegramParser.parse(plain)
            if (!parsed.hasAnyQuote()) error("JUST page loaded but no quote could be parsed")
            return parsed
        } finally {
            connection.disconnect()
        }
    }
}
