using System.IO;
using System.Text.Json;
using System.Windows;
using GoldBar.Windows.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace GoldBar.Windows.Services;

public sealed class GoldQuoteService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record QuotePair(decimal? Buy, decimal? Sell);

    public async Task<GoldQuoteResult> FetchAsync(GoldQuoteSettings input)
    {
        var settings = input.Normalize();
        if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password))
            return new GoldQuoteResult(false, null, "نام کاربری یا رمز سایت مظنه تنظیم نشده است.");

        Window? host = null;
        decimal? lastSellQuote = null;
        try
        {
            var web = new WebView2();
            host = new Window
            {
                Width = 16,
                Height = 16,
                Left = -10000,
                Top = -10000,
                Opacity = 0.01,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Content = web
            };
            host.Show();

            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GoldBar", "QuoteWebView2");
            Directory.CreateDirectory(userData);
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            web.CoreWebView2.Settings.IsStatusBarEnabled = false;

            await NavigateAsync(web, settings.Url, TimeSpan.FromSeconds(12));
            await Task.Delay(450);

            var existing = await TryExtractQuotesAsync(web);
            lastSellQuote = existing.Sell;
            if (existing.Buy is > 0)
                return Success(existing.Buy, existing.Sell);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                await TryLoginAsync(web, settings.Username, settings.Password);
                await Task.Delay(attempt == 0 ? 900 : 1200);

                for (var poll = 0; poll < 6; poll++)
                {
                    var quotes = await TryExtractQuotesAsync(web);
                    if (quotes.Sell is > 0) lastSellQuote = quotes.Sell;
                    if (quotes.Buy is > 0)
                        return Success(quotes.Buy, quotes.Sell ?? lastSellQuote);
                    await Task.Delay(450);
                }
            }

            var pageState = await GetPageStateAsync(web);
            if (pageState.Contains("رمز", StringComparison.Ordinal) &&
                (pageState.Contains("اشتباه", StringComparison.Ordinal) || pageState.Contains("نادرست", StringComparison.Ordinal)))
                return new GoldQuoteResult(false, null, "ورود به سایت ناموفق بود. نام کاربری و رمز را بررسی کنید.")
                {
                    SellQuote = lastSellQuote
                };

            return new GoldQuoteResult(false, null, "مظنه موجود نیست")
            {
                SellQuote = lastSellQuote
            };
        }
        catch (TimeoutException)
        {
            return new GoldQuoteResult(false, null, "سایت مظنه در زمان مقرر پاسخ نداد.") { SellQuote = lastSellQuote };
        }
        catch (Exception ex)
        {
            return new GoldQuoteResult(false, null, "خطا در دریافت مظنه: " + ex.Message) { SellQuote = lastSellQuote };
        }
        finally
        {
            try { host?.Close(); } catch { }
        }
    }

    private static GoldQuoteResult Success(decimal buy, decimal? sell) =>
        new(true, buy, "مظنه دریافت شد.", DateTimeOffset.Now) { SellQuote = sell };

    private static async Task NavigateAsync(WebView2 web, string url, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? _, CoreWebView2NavigationCompletedEventArgs e) => tcs.TrySetResult(e.IsSuccess);
        web.NavigationCompleted += Completed;
        try
        {
            web.Source = new Uri(url);
            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            if (done != tcs.Task) throw new TimeoutException();
            _ = await tcs.Task;
        }
        finally
        {
            web.NavigationCompleted -= Completed;
        }
    }

    private static async Task TryLoginAsync(WebView2 web, string username, string password)
    {
        if (web.CoreWebView2 is null) return;
        var u = JsonSerializer.Serialize(username, JsonOptions);
        var p = JsonSerializer.Serialize(password, JsonOptions);
        var script = $$"""
(() => {
  const username = {{u}};
  const password = {{p}};
  const visible = el => {
    const s = getComputedStyle(el);
    const r = el.getBoundingClientRect();
    return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0 && r.height > 0;
  };
  const setValue = (el, value) => {
    const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
    if (setter) setter.call(el, value); else el.value = value;
    el.dispatchEvent(new Event('input', { bubbles:true }));
    el.dispatchEvent(new Event('change', { bubbles:true }));
  };
  const inputs = [...document.querySelectorAll('input')].filter(visible);
  const pass = inputs.find(i => (i.type || '').toLowerCase() === 'password');
  const user = inputs.find(i => {
    const key = `${i.name || ''} ${i.id || ''} ${i.placeholder || ''} ${i.autocomplete || ''}`.toLowerCase();
    return i !== pass && /(phone|mobile|tel|user|username|login|موبایل|تلفن|کاربر)/.test(key);
  }) || inputs.find(i => i !== pass && !['hidden','submit','button','checkbox','radio'].includes((i.type || '').toLowerCase()));
  if (user && !String(user.value || '').trim()) setValue(user, username);
  if (pass) setValue(pass, password);
  const form = (pass || user)?.closest('form');
  const buttons = [...(form || document).querySelectorAll('button,input[type="submit"],input[type="button"]')].filter(visible);
  const preferred = buttons.find(b => /(ورود|ادامه|login|sign in|submit)/i.test(String(b.innerText || b.value || b.textContent || ''))) || buttons.find(b => b.type === 'submit') || buttons[0];
  if (preferred) preferred.click();
  else if (form) form.requestSubmit?.();
  return { user:!!user, pass:!!pass, clicked:!!preferred };
})()
""";
        try { await web.ExecuteScriptAsync(script); } catch { }
    }

    private static async Task<QuotePair> TryExtractQuotesAsync(WebView2 web)
    {
        if (web.CoreWebView2 is null) return new QuotePair(null, null);
        const string script = """
(() => {
  const fa='۰۱۲۳۴۵۶۷۸۹', ar='٠١٢٣٤٥٦٧٨٩';
  const norm = s => String(s || '')
    .replace(/[۰-۹]/g,d=>String(fa.indexOf(d)))
    .replace(/[٠-٩]/g,d=>String(ar.indexOf(d)));
  const visible = el => {
    const s=getComputedStyle(el), r=el.getBoundingClientRect();
    return s.display!=='none' && s.visibility!=='hidden' && r.width>0 && r.height>0;
  };
  const all=[...document.querySelectorAll('body *')].filter(visible);
  const nums = text => (norm(text).match(/\d[\d,٬،.\s]{4,}\d/g) || [])
    .map(raw=>Number(raw.replace(/[^0-9]/g,'')))
    .filter(n=>Number.isFinite(n) && n>=1000000);

  function readCard(labelText, otherLabel, tone) {
    const exact = all.find(el => norm(el.innerText || el.textContent || '').replace(/\s+/g,' ').trim() === labelText);
    const label = exact || all.find(el => norm(el.innerText || el.textContent || '').includes(labelText));
    if (!label) return null;

    const candidates=[];
    let node=label;
    for(let depth=0; depth<8 && node; depth++, node=node.parentElement) {
      const text=norm(node.innerText || node.textContent || '');
      if (!text.includes(labelText)) continue;
      const values=nums(text);
      if (!values.length) continue;
      const r=node.getBoundingClientRect();
      const bg=getComputedStyle(node).backgroundColor || '';
      const rgb=(bg.match(/\d+/g)||[]).slice(0,3).map(Number);
      const [red=0,green=0,blue=0]=rgb;
      const toneMatch = tone==='green' ? green > red * 1.25 && green > blue * 1.25 : red > green * 1.25 && red > blue * 1.25;
      candidates.push({
        value:Math.max(...values),
        containsOther:text.includes(otherLabel),
        toneMatch,
        area:Math.max(1,r.width*r.height),
        depth
      });
    }
    candidates.sort((a,b) =>
      Number(a.containsOther)-Number(b.containsOther) ||
      Number(b.toneMatch)-Number(a.toneMatch) ||
      a.area-b.area || a.depth-b.depth);
    return candidates[0]?.value || null;
  }

  return {
    buy: readCard('خرید از ما','فروش به ما','green'),
    sell: readCard('فروش به ما','خرید از ما','red')
  };
})()
""";
        try
        {
            var raw = await web.ExecuteScriptAsync(script);
            if (string.IsNullOrWhiteSpace(raw) || raw == "null") return new QuotePair(null, null);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            decimal? buy = root.TryGetProperty("buy", out var b) && b.ValueKind == JsonValueKind.Number && b.TryGetDecimal(out var bv) && bv > 0 ? bv : null;
            decimal? sell = root.TryGetProperty("sell", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetDecimal(out var sv) && sv > 0 ? sv : null;
            return new QuotePair(buy, sell);
        }
        catch
        {
            return new QuotePair(null, null);
        }
    }

    private static async Task<string> GetPageStateAsync(WebView2 web)
    {
        if (web.CoreWebView2 is null) return string.Empty;
        try
        {
            var raw = await web.ExecuteScriptAsync("document.body?.innerText?.slice(0,5000) || ''");
            return JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
        }
        catch { return string.Empty; }
    }
}
