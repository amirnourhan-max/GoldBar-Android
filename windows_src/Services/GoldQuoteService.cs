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

    public async Task<GoldQuoteResult> FetchAsync(GoldQuoteSettings input)
    {
        var settings = input.Normalize();
        if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password))
            return new GoldQuoteResult(false, null, "نام کاربری یا رمز سایت مظنه تنظیم نشده است.");

        Window? host = null;
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

            var existing = await TryExtractQuoteAsync(web);
            if (existing is > 0)
                return new GoldQuoteResult(true, existing, "مظنه دریافت شد.", DateTimeOffset.Now);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                await TryLoginAsync(web, settings.Username, settings.Password);
                await Task.Delay(attempt == 0 ? 900 : 1200);

                for (var poll = 0; poll < 5; poll++)
                {
                    var quote = await TryExtractQuoteAsync(web);
                    if (quote is > 0)
                        return new GoldQuoteResult(true, quote, "مظنه دریافت شد.", DateTimeOffset.Now);
                    await Task.Delay(450);
                }
            }

            var pageState = await GetPageStateAsync(web);
            if (pageState.Contains("رمز", StringComparison.Ordinal) &&
                (pageState.Contains("اشتباه", StringComparison.Ordinal) || pageState.Contains("نادرست", StringComparison.Ordinal)))
                return new GoldQuoteResult(false, null, "ورود به سایت ناموفق بود. نام کاربری و رمز را بررسی کنید.");

            return new GoldQuoteResult(false, null, "مظنه موجود نیست");
        }
        catch (TimeoutException)
        {
            return new GoldQuoteResult(false, null, "سایت مظنه در زمان مقرر پاسخ نداد.");
        }
        catch (Exception ex)
        {
            return new GoldQuoteResult(false, null, "خطا در دریافت مظنه: " + ex.Message);
        }
        finally
        {
            try { host?.Close(); } catch { }
        }
    }

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

    private static async Task<decimal?> TryExtractQuoteAsync(WebView2 web)
    {
        if (web.CoreWebView2 is null) return null;
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
  const label=all.find(el => norm(el.innerText || el.textContent).includes('خرید از ما'));
  if (!label) return null;
  const candidates=[];
  let node=label;
  for(let i=0;i<7 && node;i++,node=node.parentElement){
    const text=norm(node.innerText || node.textContent || '');
    if (!text.includes('خرید از ما')) continue;
    const bg=getComputedStyle(node).backgroundColor || '';
    const green=/rgb\(\s*(?:0|[1-9]\d?|1\d\d)\s*,\s*(?:8\d|9\d|1\d\d|2[0-2]\d)\s*,/.test(bg) || /green/i.test(node.className || '');
    candidates.push({node,text,green,area:Math.max(1,node.getBoundingClientRect().width*node.getBoundingClientRect().height)});
  }
  candidates.sort((a,b)=>(Number(b.green)-Number(a.green)) || (a.area-b.area));
  const box=candidates[0]?.node || label.parentElement || label;
  const text=norm(box.innerText || box.textContent || '');
  const matches=text.match(/\d[\d,٬،.\s]{4,}\d/g) || [];
  const numbers=matches.map(raw=>Number(raw.replace(/[^0-9]/g,''))).filter(n=>Number.isFinite(n) && n>=1000000);
  if (!numbers.length) return null;
  return Math.max(...numbers);
})()
""";
        try
        {
            var raw = await web.ExecuteScriptAsync(script);
            if (string.IsNullOrWhiteSpace(raw) || raw == "null") return null;
            if (decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0)
                return value;
        }
        catch { }
        return null;
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
