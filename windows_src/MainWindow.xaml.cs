using System.IO;
using System.Text.Json;
using System.Windows;
using GoldBar.Windows.Models;
using GoldBar.Windows.Services;
using Microsoft.Web.WebView2.Core;

namespace GoldBar.Windows;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly ScaleService _scale = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly bool _runUiSelfTest;
    private bool _uiSelfTestStarted;
    private ScaleSettings _settings = ScaleSettings.Defaults();

    public MainWindow(bool runUiSelfTest = false)
    {
        _runUiSelfTest = runUiSelfTest;
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => _scale.Dispose();
        _scale.WeightReceived += (value, raw) => PostEvent("scale:weight", new { value, raw, decimals = _settings.Decimals });
        _scale.StatusChanged += (connected, message) => PostEvent("scale:status", new { connected, message });
        _scale.Error += message => PostEvent("scale:error", new { message });
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsStore.LoadAsync();
        try
        {
            await Web.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            if (_runUiSelfTest)
            {
                Console.Error.WriteLine("UI-SELF-TEST: WebView2 initialization failed: " + ex.Message);
                Application.Current.Shutdown(1);
                return;
            }
            MessageBox.Show(
                "Microsoft Edge WebView2 Runtime is required to run Gold Bar.\n\n" + ex.Message,
                "Gold Bar", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        Web.CoreWebView2.Settings.AreDevToolsEnabled = false;
        Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
        Web.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        Web.NavigationCompleted += OnNavigationCompleted;

        var renderer = Path.Combine(AppContext.BaseDirectory, "Renderer");
        Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.goldbar", renderer, CoreWebView2HostResourceAccessKind.DenyCors);
        Web.Source = new Uri("https://app.goldbar/index.html");

        if (_settings.AutoRead && !_runUiSelfTest)
            await _scale.ConnectAsync(_settings);
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!_runUiSelfTest || _uiSelfTestStarted) return;
        _uiSelfTestStarted = true;

        try
        {
            await Task.Delay(250);
            const string script = """
(() => {
  const base = window.__goldbarSelfTest ? window.__goldbarSelfTest() : { ok: false };
  const title = () => document.querySelector('.dash-title span:last-child')?.textContent?.trim() || '';
  const nav = [...document.querySelectorAll('.nav-item')];
  const clickNav = label => {
    const btn = nav.find(b => (b.textContent || '').includes(label));
    if (!btn) return false;
    btn.click();
    return btn.classList.contains('active');
  };

  const meltsClicked = clickNav('آبشده‌ها');
  const meltsNav = meltsClicked && title() === 'آبشده‌ها' && document.querySelector('#pageHost')?.classList.contains('active');

  const registerClicked = clickNav('ثبت آبشده');
  const weight = document.querySelector('#weightInput');
  const assay = document.querySelector('#purityInput');
  weight.value = '12.345';
  weight.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  const enterMovesToAssay = document.activeElement === assay;
  assay.value = '750';
  assay.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));

  clickNav('آبشده‌ها');
  const meltsText = document.querySelector('#pageHost')?.textContent || '';
  const quickSaved = meltsText.includes('12.345') && meltsText.includes('750');

  const reportsClicked = clickNav('گزارش‌ها');
  const reportsWork = reportsClicked && title() === 'گزارش‌ها' && (document.querySelector('#pageHost')?.textContent || '').includes('تعداد آبشده‌ها');

  const settingsClicked = clickNav('تنظیمات');
  const settingsWork = settingsClicked && title() === 'تنظیمات';

  const dashboardClicked = clickNav('داشبورد');
  const dashboardWork = dashboardClicked && title() === 'داشبورد';

  return {
    ok: Boolean(base.ok && meltsNav && registerClicked && enterMovesToAssay && quickSaved && reportsWork && settingsWork && dashboardWork),
    base, meltsNav, registerClicked, enterMovesToAssay, quickSaved, reportsWork, settingsWork, dashboardWork
  };
})()
""";

            var json = await Web.ExecuteScriptAsync(script);
            using var doc = JsonDocument.Parse(json);
            var ok = doc.RootElement.TryGetProperty("ok", out var okElement) && okElement.GetBoolean();
            Console.WriteLine("UI-SELF-TEST: " + json);
            Application.Current.Shutdown(ok ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("UI-SELF-TEST: FAIL: " + ex);
            Application.Current.Shutdown(1);
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? id = null;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            if (root.GetProperty("kind").GetString() != "request") return;
            id = root.GetProperty("id").GetString();
            var action = root.GetProperty("action").GetString() ?? string.Empty;
            var payload = root.TryGetProperty("payload", out var p) ? p : default;
            object? result = action switch
            {
                "window:minimize" => Do(() => WindowState = System.Windows.WindowState.Minimized),
                "window:maximizeToggle" => Do(() => WindowState = WindowState == System.Windows.WindowState.Maximized ? System.Windows.WindowState.Normal : System.Windows.WindowState.Maximized),
                "window:close" => Do(Close),
                "settings:get" => _settings,
                "settings:save" => await SaveSettingsAsync(payload),
                "settings:reset" => await ResetSettingsAsync(),
                "scale:connect" => new { ok = await _scale.ConnectAsync(_settings) },
                "scale:disconnect" => Do(() => _scale.Disconnect()),
                "scale:read" => new { ok = await EnsureAndReadScaleAsync() },
                _ => throw new InvalidOperationException($"Unknown action: {action}")
            };
            Reply(id, true, result, null);
        }
        catch (Exception ex)
        {
            Reply(id, false, null, ex.Message);
        }
    }

    private async Task<ScaleSettings> SaveSettingsAsync(JsonElement payload)
    {
        var next = payload.Deserialize<ScaleSettings>(_json) ?? ScaleSettings.Defaults();
        _settings = await _settingsStore.SaveAsync(next);
        if (_scale.IsConnected) await _scale.ConnectAsync(_settings);
        else _scale.ApplySettings(_settings);
        return _settings;
    }

    private async Task<ScaleSettings> ResetSettingsAsync()
    {
        _settings = await _settingsStore.ResetAsync();
        if (_scale.IsConnected) await _scale.ConnectAsync(_settings);
        return _settings;
    }

    private async Task<bool> EnsureAndReadScaleAsync()
    {
        if (!_scale.IsConnected && !await _scale.ConnectAsync(_settings)) return false;
        return await _scale.RequestWeightAsync();
    }

    private object Do(Action action) { action(); return new { ok = true }; }

    private void Reply(string? id, bool ok, object? data, string? error)
    {
        if (string.IsNullOrWhiteSpace(id) || Web.CoreWebView2 is null) return;
        var json = JsonSerializer.Serialize(new { kind = "response", id, ok, data, error }, _json);
        Web.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void PostEvent(string name, object data)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (Web.CoreWebView2 is null) return;
            var json = JsonSerializer.Serialize(new { kind = "event", @event = name, data }, _json);
            Web.CoreWebView2.PostWebMessageAsJson(json);
        });
    }
}
