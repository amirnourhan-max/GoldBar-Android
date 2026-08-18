package com.amirnourhan.zarpulse.data

object TelegramParser {
    data class Parsed(
        val abshodeh: Quote? = null,
        val gram: Quote? = null,
        val coinCash: Quote? = null,
        val coinHavaleh: Quote? = null
    ) {
        fun hasAnyQuote(): Boolean = listOf(abshodeh, gram, coinCash, coinHavaleh).any { it?.value != null }
    }

    private val digitMap = mapOf(
        '۰' to '0', '۱' to '1', '۲' to '2', '۳' to '3', '۴' to '4',
        '۵' to '5', '۶' to '6', '۷' to '7', '۸' to '8', '۹' to '9',
        '٠' to '0', '١' to '1', '٢' to '2', '٣' to '3', '٤' to '4',
        '٥' to '5', '٦' to '6', '٧' to '7', '٨' to '8', '٩' to '9'
    )

    private fun normalizeDigits(text: String): String = buildString(text.length) {
        text.forEach { append(digitMap[it] ?: it) }
    }

    private fun normalizeText(text: String): String = text
        .replace('\u200c', ' ')
        .replace('\u200d', ' ')
        .replace('\u200e', ' ')
        .replace('\u200f', ' ')
        .replace('\u00a0', ' ')
        .replace('ـ', ' ')
        .lineSequence()
        .joinToString("\n") { line -> line.replace(Regex("[ \\t]+"), " ").trim() }

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

    private fun lastQuote(text: String, labelPattern: String, label: String): Quote? {
        val number = "([0-9۰-۹٠-٩][0-9۰-۹٠-٩,٬ ]{4,})"
        val regex = Regex(
            "([🔺🔻])?\\s*#?\\s*$labelPattern\\s*[:：]?\\s*([🔺🔻])?\\s*$number",
            RegexOption.IGNORE_CASE
        )

        val match = text.lineSequence().mapNotNull { regex.find(it) }.lastOrNull() ?: return null
        val arrow = match.groupValues[1].ifBlank { match.groupValues[2] }
        val value = valueOf(match.groupValues[3]) ?: return null
        return Quote(label, value, "تومان", directionOf(arrow), "JUST")
    }

    fun parse(rawText: String): Parsed {
        val text = normalizeText(rawText)
        return Parsed(
            abshodeh = lastQuote(text, "(?:ا|آ)بش\\s*ده\\s*حواله", "آبشده حواله"),
            gram = lastQuote(text, "گرم\\s*طلا", "گرم طلا"),
            coinCash = lastQuote(text, "سکه\\s*نقدی", "سکه نقدی"),
            coinHavaleh = lastQuote(text, "سکه\\s*حواله", "سکه حواله")
        )
    }
}
