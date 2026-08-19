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
    private ScaleSettings _settings = ScaleSettings.Defaults();

    public MainWindow()
    {
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

        var renderer = Path.Combine(AppContext.BaseDirectory, "Renderer");
        Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.goldbar", renderer, CoreWebView2HostResourceAccessKind.DenyCors);
        Web.Source = new Uri("https://app.goldbar/index.html");

        if (_settings.AutoRead) await _scale.ConnectAsync(_settings);
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
