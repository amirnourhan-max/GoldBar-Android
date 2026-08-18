package com.amirnourhan.zarpulse.data

object TelegramParser {
    data class Parsed(
        val abshodeh: Quote? = null,
        val gram: Quote? = null,
        val coinCash: Quote? = null,
        val coinHavaleh: Quote? = null
    )

    private val digitMap = mapOf(
        '۰' to '0', '۱' to '1', '۲' to '2', '۳' to '3', '۴' to '4',
        '۵' to '5', '۶' to '6', '۷' to '7', '۸' to '8', '۹' to '9',
        '٠' to '0', '١' to '1', '٢' to '2', '٣' to '3', '٤' to '4',
        '٥' to '5', '٦' to '6', '٧' to '7', '٨' to '8', '٩' to '9'
    )

    private fun normalizeDigits(text: String): String = buildString(text.length) {
        text.forEach { append(digitMap[it] ?: it) }
    }

    private fun valueOf(text: String): Long? = normalizeDigits(text)
        .replace(",", "")
        .replace("٬", "")
        .replace(" ", "")
        .filter { it.isDigit() }
        .toLongOrNull()

    private fun directionOf(token: String?): Int = when (token) {
        "🔺" -> 1
        "🔻" -> -1
        else -> 0
    }

    private fun lastQuote(
        text: String,
        regex: Regex,
        label: String
    ): Quote? {
        val match = text.lineSequence().mapNotNull { regex.find(it) }.lastOrNull() ?: return null
        val arrow = match.groups["pre"]?.value ?: match.groups["post"]?.value
        val value = valueOf(match.groups["value"]?.value.orEmpty()) ?: return null
        return Quote(label, value, "تومان", directionOf(arrow), "JUST")
    }

    fun parse(text: String): Parsed {
        val flags = setOf(RegexOption.IGNORE_CASE)
        val number = "(?<value>[0-9۰-۹٠-٩,٬]+)"
        val pre = "(?<pre>[🔺🔻])?"
        val between = "\\s*:?\\s*(?<post>[🔺🔻])?\\s*"

        fun pattern(label: String) = Regex(
            "$pre\\s*#?$label$between$number\\s*[🔺🔻]?",
            flags
        )

        return Parsed(
            abshodeh = lastQuote(text, pattern("ابش[ـ‌\\s-]*ده[‌\\s-]*حواله"), "آبشده حواله"),
            gram = lastQuote(text, pattern("گرم[‌\\s-]*طلا"), "گرم طلا"),
            coinCash = lastQuote(text, pattern("سکه[‌\\s-]*نقدی"), "سکه نقدی"),
            coinHavaleh = lastQuote(text, pattern("سکه[‌\\s-]*حواله"), "سکه حواله")
        )
    }
}
