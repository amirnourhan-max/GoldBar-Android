package com.amirnourhan.zarpulse.data

import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL

class TalaApiSource {
    data class Result(
        val gold18: Quote? = null,
        val usd: Quote? = null,
        val eur: Quote? = null
    )

    fun fetch(apiKey: String): Result {
        require(apiKey.isNotBlank()) { "Tala API key is empty" }
        val connection = (URL("https://api.tala.ir/v1/rates/gold18,usd,eur").openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            connectTimeout = 12_000
            readTimeout = 12_000
            setRequestProperty("x-api-key", apiKey.trim())
            setRequestProperty("Accept", "application/json")
        }

        try {
            val code = connection.responseCode
            val stream = if (code in 200..299) connection.inputStream else connection.errorStream
            val body = stream?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }.orEmpty()
            if (code !in 200..299) error("Tala API HTTP $code: $body")

            val root = JSONObject(body)
            if (!root.optBoolean("success", false)) error("Tala API returned success=false")
            val rates = root.optJSONArray("rates") ?: return Result()

            var gold18: Quote? = null
            var usd: Quote? = null
            var eur: Quote? = null

            for (i in 0 until rates.length()) {
                val item = rates.getJSONObject(i)
                val key = item.optString("key")
                val value = item.optLong("value", -1L).takeIf { it >= 0 }
                val unit = item.optString("unit", "تومان")
                if (value == null) continue
                when (key) {
                    "gold18" -> gold18 = Quote("طلای ۱۸ عیار", value, unit, 0, "Tala.ir")
                    "usd" -> usd = Quote("دلار", value, unit, 0, "Tala.ir")
                    "eur" -> eur = Quote("یورو", value, unit, 0, "Tala.ir")
                }
            }
            return Result(gold18, usd, eur)
        } finally {
            connection.disconnect()
        }
    }
}
