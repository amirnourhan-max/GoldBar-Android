package com.amirnourhan.zarpulse.widget

import android.appwidget.AppWidgetManager
import android.content.Context
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.DpSize
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.glance.GlanceId
import androidx.glance.GlanceModifier
import androidx.glance.LocalSize
import androidx.glance.action.ActionParameters
import androidx.glance.action.clickable
import androidx.glance.appwidget.GlanceAppWidget
import androidx.glance.appwidget.GlanceAppWidgetReceiver
import androidx.glance.appwidget.SizeMode
import androidx.glance.appwidget.action.ActionCallback
import androidx.glance.appwidget.action.actionRunCallback
import androidx.glance.appwidget.cornerRadius
import androidx.glance.appwidget.provideContent
import androidx.glance.background
import androidx.glance.layout.Alignment
import androidx.glance.layout.Column
import androidx.glance.layout.Row
import androidx.glance.layout.Spacer
import androidx.glance.layout.fillMaxSize
import androidx.glance.layout.fillMaxWidth
import androidx.glance.layout.height
import androidx.glance.layout.padding
import androidx.glance.text.FontWeight
import androidx.glance.text.Text
import androidx.glance.text.TextStyle
import androidx.glance.unit.ColorProvider
import com.amirnourhan.zarpulse.data.MarketRepository
import com.amirnourhan.zarpulse.data.MarketSnapshot
import com.amirnourhan.zarpulse.data.MarketStore
import com.amirnourhan.zarpulse.data.MarketWorker
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

class MarketWidget : GlanceAppWidget() {
    override val sizeMode: SizeMode = SizeMode.Responsive(
        setOf(
            DpSize(180.dp, 105.dp),
            DpSize(320.dp, 145.dp),
            DpSize(320.dp, 205.dp)
        )
    )

    override suspend fun provideGlance(context: Context, id: GlanceId) {
        val snapshot = MarketStore(context).load()
        provideContent { WidgetContent(snapshot) }
    }

    @Composable
    private fun WidgetContent(snapshot: MarketSnapshot) {
        val size = LocalSize.current
        val medium = size.height >= 135.dp
        val tall = size.height >= 185.dp

        val bg = ColorProvider(Color(0xFF0D1117))
        val gold = ColorProvider(Color(0xFFE0BD55))
        val primary = ColorProvider(Color(0xFFF5F7FA))
        val muted = ColorProvider(Color(0xFF8D96A5))
        val green = ColorProvider(Color(0xFF4DE39A))
        val red = ColorProvider(Color(0xFFFF7E86))
        val cyan = ColorProvider(Color(0xFF35D6F4))

        Column(
            modifier = GlanceModifier
                .fillMaxSize()
                .background(bg)
                .cornerRadius(24.dp)
                .padding(horizontal = 16.dp, vertical = 12.dp)
        ) {
            Row(
                modifier = GlanceModifier.fillMaxWidth(),
                verticalAlignment = Alignment.Vertical.CenterVertically
            ) {
                Text(
                    text = "ZAR PULSE",
                    style = TextStyle(
                        color = gold,
                        fontWeight = FontWeight.Bold,
                        fontSize = 17.sp
                    )
                )
                Spacer(GlanceModifier.defaultWeight())
                Text(
                    text = if (snapshot.telegramOk) "● JUST" else "● JUST",
                    style = TextStyle(
                        color = if (snapshot.telegramOk) green else muted,
                        fontWeight = FontWeight.Bold,
                        fontSize = 11.sp
                    )
                )
                Text(
                    text = "  ↻",
                    modifier = GlanceModifier
                        .padding(horizontal = 7.dp, vertical = 4.dp)
                        .clickable(actionRunCallback<RefreshAction>()),
                    style = TextStyle(
                        color = primary,
                        fontWeight = FontWeight.Bold,
                        fontSize = 18.sp
                    )
                )
            }

            Spacer(GlanceModifier.height(7.dp))
            QuoteRow("آبشده", snapshot.abshodeh.value, snapshot.abshodeh.direction, primary, muted, green, red)
            QuoteRow("گرم طلا", snapshot.gramTelegram.value, snapshot.gramTelegram.direction, primary, muted, green, red)

            if (medium) {
                QuoteRow("سکه نقدی", snapshot.coinCash.value, snapshot.coinCash.direction, primary, muted, green, red)
            }

            if (tall) {
                QuoteRow(
                    "دلار",
                    snapshot.usd.value,
                    snapshot.usd.direction,
                    primary,
                    muted,
                    green,
                    red,
                    missingText = if (snapshot.talaOk) "—" else "Tala API"
                )
            }

            Spacer(GlanceModifier.height(7.dp))
            val footer = if (snapshot.updatedAt > 0) {
                val time = SimpleDateFormat("HH:mm:ss", Locale.getDefault()).format(Date(snapshot.updatedAt))
                "بروزرسانی $time"
            } else {
                "در حال دریافت نرخ..."
            }
            Text(
                text = footer,
                style = TextStyle(
                    color = if (snapshot.updatedAt > 0) cyan else muted,
                    fontSize = 11.sp,
                    fontWeight = FontWeight.Medium
                )
            )
        }
    }

    @Composable
    private fun QuoteRow(
        label: String,
        value: Long?,
        direction: Int,
        primary: ColorProvider,
        muted: ColorProvider,
        green: ColorProvider,
        red: ColorProvider,
        missingText: String = "—"
    ) {
        Row(
            modifier = GlanceModifier.fillMaxWidth().padding(vertical = 3.dp),
            verticalAlignment = Alignment.Vertical.CenterVertically
        ) {
            Text(
                text = label,
                style = TextStyle(color = muted, fontSize = 14.sp, fontWeight = FontWeight.Medium)
            )
            Spacer(GlanceModifier.defaultWeight())

            val arrow = when {
                direction > 0 -> "▲ "
                direction < 0 -> "▼ "
                else -> ""
            }
            if (arrow.isNotEmpty()) {
                Text(
                    text = arrow,
                    style = TextStyle(
                        color = if (direction > 0) green else red,
                        fontSize = 14.sp,
                        fontWeight = FontWeight.Bold
                    )
                )
            }

            val number = value?.let { NumberFormat.getNumberInstance(Locale.US).format(it) } ?: missingText
            Text(
                text = number,
                style = TextStyle(
                    color = if (value != null) primary else muted,
                    fontWeight = FontWeight.Bold,
                    fontSize = if (value != null) 17.sp else 12.sp
                )
            )
        }
    }
}

class MarketWidgetReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = MarketWidget()

    override fun onEnabled(context: Context) {
        super.onEnabled(context)
        MarketWorker.schedule(context)
        MarketWorker.refreshNow(context)
    }

    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray
    ) {
        super.onUpdate(context, appWidgetManager, appWidgetIds)
        MarketWorker.refreshNow(context)
    }
}

class RefreshAction : ActionCallback {
    override suspend fun onAction(
        context: Context,
        glanceId: GlanceId,
        parameters: ActionParameters
    ) {
        MarketRepository(context).refresh()
    }
}
