package com.amirnourhan.zarpulse

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.glance.appwidget.GlanceAppWidgetManager
import com.amirnourhan.zarpulse.data.MarketRepository
import com.amirnourhan.zarpulse.data.MarketSnapshot
import com.amirnourhan.zarpulse.data.MarketStore
import com.amirnourhan.zarpulse.data.MarketWorker
import com.amirnourhan.zarpulse.data.Quote
import com.amirnourhan.zarpulse.ui.theme.ZarPulseTheme
import com.amirnourhan.zarpulse.widget.MarketWidgetReceiver
import kotlinx.coroutines.launch
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        MarketWorker.schedule(this)
        setContent {
            ZarPulseTheme {
                MarketScreen()
            }
        }
    }

    @Composable
    private fun MarketScreen() {
        val context = this
        val store = remember { MarketStore(context) }
        val repository = remember { MarketRepository(context) }
        val scope = rememberCoroutineScope()
        var snapshot by remember { mutableStateOf(store.load()) }
        var apiKey by remember { mutableStateOf(store.talaApiKey) }
        var loading by remember { mutableStateOf(false) }
        var message by remember { mutableStateOf("برای دریافت دلار و طلای Tala، کلید API را وارد کنید.") }

        fun refresh() {
            scope.launch {
                loading = true
                runCatching { repository.refresh() }
                    .onSuccess {
                        snapshot = it
                        message = buildString {
                            append(if (it.telegramOk) "JUST ✓" else "JUST ✕")
                            append("   ")
                            append(if (it.talaOk) "Tala ✓" else if (apiKey.isBlank()) "Tala: بدون کلید" else "Tala ✕")
                        }
                    }
                    .onFailure { message = it.message ?: "خطا در بروزرسانی" }
                loading = false
            }
        }

        LaunchedEffect(Unit) {
            if (snapshot.updatedAt == 0L) refresh()
        }

        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background)
                .padding(horizontal = 18.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Spacer(Modifier.height(24.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(Modifier.weight(1f)) {
                        Text("ZAR PULSE", color = MaterialTheme.colorScheme.primary, fontWeight = FontWeight.Black, fontSize = 28.sp)
                        Text("بازار طلا و ارز", color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    if (loading) CircularProgressIndicator(Modifier.size(28.dp), strokeWidth = 3.dp)
                }
            }

            item {
                HeroCard(snapshot)
            }

            items(
                listOf(
                    snapshot.gold18,
                    snapshot.gramTelegram,
                    snapshot.coinCash,
                    snapshot.coinHavaleh,
                    snapshot.usd,
                    snapshot.eur
                )
            ) { quote ->
                QuoteCard(quote)
            }

            item {
                Card(
                    shape = RoundedCornerShape(24.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                ) {
                    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                        Text("اتصال Tala.ir", fontWeight = FontWeight.Bold, fontSize = 18.sp)
                        OutlinedTextField(
                            value = apiKey,
                            onValueChange = { apiKey = it },
                            modifier = Modifier.fillMaxWidth(),
                            label = { Text("x-api-key") },
                            visualTransformation = PasswordVisualTransformation(),
                            singleLine = true
                        )
                        Button(
                            onClick = {
                                store.talaApiKey = apiKey
                                refresh()
                            },
                            modifier = Modifier.fillMaxWidth(),
                            colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.primary)
                        ) {
                            Text("ذخیره و بروزرسانی", color = MaterialTheme.colorScheme.onPrimary, fontWeight = FontWeight.Bold)
                        }
                    }
                }
            }

            item {
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    Button(onClick = { refresh() }, modifier = Modifier.weight(1f)) {
                        Text("↻ بروزرسانی")
                    }
                    Button(
                        onClick = {
                            scope.launch {
                                val sent = GlanceAppWidgetManager(context).requestPinGlanceAppWidget(MarketWidgetReceiver::class.java)
                                message = if (sent) "درخواست افزودن ویجت ارسال شد" else "لانچر این قابلیت را پشتیبانی نمی‌کند"
                            }
                        },
                        modifier = Modifier.weight(1f)
                    ) {
                        Text("＋ افزودن ویجت")
                    }
                }
            }

            item {
                Text(message, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(vertical = 4.dp))
                val time = if (snapshot.updatedAt > 0) SimpleDateFormat("yyyy/MM/dd  HH:mm:ss", Locale.getDefault()).format(Date(snapshot.updatedAt)) else "—"
                Text("آخرین بروزرسانی: $time", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 12.sp)
                Text("منبع آبشده/سکه: JUST @abshdh   •   منبع API: Tala.ir", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 12.sp)
                Spacer(Modifier.height(28.dp))
            }
        }
    }

    @Composable
    private fun HeroCard(snapshot: MarketSnapshot) {
        Card(
            shape = RoundedCornerShape(30.dp),
            colors = CardDefaults.cardColors(containerColor = Color(0xFF171A20))
        ) {
            Column(Modifier.padding(20.dp)) {
                Text("آبشده حواله", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 14.sp)
                Spacer(Modifier.height(6.dp))
                Text(
                    text = format(snapshot.abshodeh.value),
                    fontSize = 34.sp,
                    fontWeight = FontWeight.Black,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Row(verticalAlignment = Alignment.CenterVertically) {
                    val arrow = when {
                        snapshot.abshodeh.direction > 0 -> "▲"
                        snapshot.abshodeh.direction < 0 -> "▼"
                        else -> "•"
                    }
                    Text("$arrow  JUST IN TIME", color = MaterialTheme.colorScheme.primary, fontWeight = FontWeight.Bold)
                }
            }
        }
    }

    @Composable
    private fun QuoteCard(quote: Quote) {
        Card(
            shape = RoundedCornerShape(22.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth().padding(16.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(Modifier.weight(1f)) {
                    Text(quote.label, fontWeight = FontWeight.SemiBold)
                    Text(quote.source.ifBlank { "در انتظار داده" }, color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 12.sp)
                }
                Column(horizontalAlignment = Alignment.End) {
                    Text(format(quote.value), fontWeight = FontWeight.Bold, fontSize = 20.sp)
                    Text(quote.unit, color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 11.sp)
                }
            }
        }
    }

    private fun format(value: Long?): String = value?.let {
        NumberFormat.getNumberInstance(Locale.US).format(it)
    } ?: "—"
}
