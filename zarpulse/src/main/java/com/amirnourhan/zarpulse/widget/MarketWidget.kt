package com.amirnourhan.zarpulse.widget

import android.content.Context
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.DpSize
import androidx.compose.ui.unit.dp
import androidx.glance.GlanceId
import androidx.glance.GlanceModifier
import androidx.glance.LocalSize
import androidx.glance.action.ActionParameters
import androidx.glance.action.clickable
import androidx.glance.appwidget.GlanceAppWidget
import androidx.glance.appwidget.GlanceAppWidgetReceiver
import androidx.glance.appwidget.SizeMode
import androidx.glance.appwidget.provideContent
import androidx.glance.appwidget.action.ActionCallback
import androidx.glance.appwidget.action.actionRunCallback
import androidx.glance.appwidget.cornerRadius
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
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

class MarketWidget : GlanceAppWidget() {
    override val sizeMode: SizeMode = SizeMode.Responsive(
        setOf(DpSize(180.dp, 110.dp), DpSize(320.dp, 180.dp))
    )

    override suspend fun provideGlance(context: Context, id: GlanceId) {
        val snapshot = MarketStore(context).load()
        provideContent { WidgetContent(snapshot) }
    }

    @Composable
    private fun WidgetContent(snapshot: MarketSnapshot) {
        val size = LocalSize.current
        val wide = size.width >= 280.dp || size.height >= 160.dp
        val bg = ColorProvider(Color(0xFF101318))
        val gold = ColorProvider(Color(0xFFD8B85C))
        val primary = ColorProvider(Color(0xFFF4F1E8))
        val muted = ColorProvider(Color(0xFF9298A2))

        Column(
            modifier = GlanceModifier
                .fillMaxSize()
                .background(bg)
                .cornerRadius(24.dp)
                .padding(16.dp)
        ) {
            Row(
                modifier = GlanceModifier.fillMaxWidth(),
                verticalAlignment = Alignment.Vertical.CenterVertically
            ) {
                Text(
                    text = "ZAR PULSE",
                    style = TextStyle(color = gold, fontWeight = FontWeight.Bold)
                )
                Spacer(GlanceModifier.defaultWeight())
                Text(
                    text = "↻",
                    modifier = GlanceModifier
                        .padding(horizontal = 8.dp, vertical = 4.dp)
                        .clickable(actionRunCallback<RefreshAction>()),
                    style = TextStyle(color = primary, fontWeight = FontWeight.Bold)
                )
            }
            Spacer(GlanceModifier.height(8.dp))
            QuoteRow("آبشده", snapshot.abshodeh.value, snapshot.abshodeh.direction, primary, muted)
            QuoteRow("طلای ۱۸", snapshot.gold18.value, snapshot.gold18.direction, primary, muted)
            if (wide) {
                QuoteRow("دلار", snapshot.usd.value, snapshot.usd.direction, primary, muted)
                QuoteRow("سکه نقدی", snapshot.coinCash.value, snapshot.coinCash.direction, primary, muted)
            }
            Spacer(GlanceModifier.defaultWeight())
            val time = if (snapshot.updatedAt > 0) {
                SimpleDateFormat("HH:mm", Locale.getDefault()).format(Date(snapshot.updatedAt))
            } else "--:--"
            Text("آخرین بروزرسانی $time", style = TextStyle(color = muted))
        }
    }

    @Composable
    private fun QuoteRow(
        label: String,
        value: Long?,
        direction: Int,
        primary: ColorProvider,
        muted: ColorProvider
    ) {
        Row(
            modifier = GlanceModifier.fillMaxWidth().padding(vertical = 3.dp),
            verticalAlignment = Alignment.Vertical.CenterVertically
        ) {
            Text(label, style = TextStyle(color = muted))
            Spacer(GlanceModifier.defaultWeight())
            val arrow = when {
                direction > 0 -> "▲ "
                direction < 0 -> "▼ "
                else -> ""
            }
            val number = value?.let { NumberFormat.getNumberInstance(Locale.US).format(it) } ?: "—"
            Text("$arrow$number", style = TextStyle(color = primary, fontWeight = FontWeight.Bold))
        }
    }
}

class MarketWidgetReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = MarketWidget()
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
