package com.amirnourhan.zarpulse.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val ZarColors = darkColorScheme(
    primary = Color(0xFFD8B85C),
    onPrimary = Color(0xFF16130A),
    background = Color(0xFF0B0D10),
    onBackground = Color(0xFFF4F1E8),
    surface = Color(0xFF12161C),
    onSurface = Color(0xFFF4F1E8),
    surfaceVariant = Color(0xFF1A2028),
    onSurfaceVariant = Color(0xFFA5ABB4)
)

@Composable
fun ZarPulseTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = ZarColors,
        content = content
    )
}
