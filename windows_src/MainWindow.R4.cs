using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using GoldBar.Windows.Core;
using GoldBar.Windows.Models;
using GoldBar.Windows.Services;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace GoldBar.Windows;

public partial class MainWindow
{
    private readonly ReportImportService _r4ReportImportService = new();
    private bool _r4MessageHooked;
    private bool _r4CloseApproved;
    private bool _r4ClosingBusy;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        WindowState = R4WindowPolicy.StartupState(_runUiSelfTest);
        if (!_runUiSelfTest) R4ApplyInitialWindowBounds();
        Closing += R4OnClosing;
        Web.NavigationCompleted += R4OnNavigationCompleted;
    }

    private void R4ApplyInitialWindowBounds()
    {
        var work = SystemParameters.WorkArea;
        var targetWidth = Math.Min(1536d, work.Width * 0.88d);
        var targetHeight = Math.Min(1024d, work.Height * 0.88d);
        Width = Math.Max(MinWidth, Math.Min(targetWidth, Math.Max(MinWidth, work.Width - 24d)));
        Height = Math.Max(MinHeight, Math.Min(targetHeight, Math.Max(MinHeight, work.Height - 24d)));
        Left = work.Left + Math.Max(0d, (work.Width - Width) / 2d);
        Top = work.Top + Math.Max(0d, (work.Height - Height) / 2d);
    }

    private async void R4OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (Web.CoreWebView2 is null) return;
        if (!_r4MessageHooked)
        {
            Web.CoreWebView2.WebMessageReceived += R4OnWebMessageReceived;
            _r4MessageHooked = true;
        }

        try
        {
            var selfTest = _runUiSelfTest ? "true" : "false";
            var script =
                "window.__goldbarR4SelfTest = " + selfTest + ";\n" +
                "if (!document.querySelector('script[data-goldbar-r4]')) {\n" +
                "  const s = document.createElement('script');\n" +
                "  s.src = 'r4.js';\n" +
                "  s.dataset.goldbarR4 = '1';\n" +
                "  document.body.appendChild(s);\n" +
                "}\n";
            await Web.ExecuteScriptAsync(script);
        }
        catch { }
    }

    private async void R4OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? id = null;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("kind", out var kind) || kind.GetString() != "r4request") return;
            id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var action = root.TryGetProperty("action", out var actionElement) ? actionElement.GetString() : string.Empty;
            var payload = root.TryGetProperty("payload", out var p) ? p : default;
            object result = action switch
            {
                "report:import" => await R4ImportReportAsync(),
                "scale:test" => await R4TestScaleAsync(payload),
                _ => throw new InvalidOperationException($"Unknown r4 action: {action}")
            };
            R4Reply(id, true, result, null);
        }
        catch (Exception ex)
        {
            R4Reply(id, false, null, ex.Message);
        }
    }

    private async Task<object> R4TestScaleAsync(JsonElement payload)
    {
        ScaleSettings candidate;
        try
        {
            candidate = payload.ValueKind == JsonValueKind.Object
                ? payload.Deserialize<ScaleSettings>(_json) ?? _settings
                : _settings;
        }
        catch
        {
            candidate = _settings;
        }
        return await _scale.TestAsync(candidate, 1400);
    }

    private Task<object> R4ImportReportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "وارد کردن گزارش Gold Bar",
            Filter = "Gold Bar Excel Report (*.xlsx)|*.xlsx|Excel Workbook (*.xlsx)|*.xlsx",
            Multiselect = false,
            CheckFileExists = true,
            InitialDirectory = Directory.Exists(_settings.ReportDirectory)
                ? _settings.ReportDirectory
                : ScaleSettings.GetDefaultReportDirectory()
        };

        if (dialog.ShowDialog(this) != true)
            return Task.FromResult<object>(new { ok = false, cancelled = true });

        var request = _r4ReportImportService.LoadXlsx(dialog.FileName);
        return Task.FromResult<object>(new
        {
            ok = true,
            path = dialog.FileName,
            count = request.Entries.Count,
            entries = request.Entries
        });
    }

    private async void R4OnClosing(object? sender, CancelEventArgs e)
    {
        if (_runUiSelfTest || _r4CloseApproved) return;
        e.Cancel = true;
        if (_r4ClosingBusy) return;
        _r4ClosingBusy = true;

        try
        {
            var choice = MessageBox.Show(
                this,
                "آیا می‌خواهید قبل از بستن نرم‌افزار گزارش آبشده‌های این جلسه ذخیره شود؟\n\nبله: ذخیره گزارش و خروج\nخیر: خروج بدون ذخیره\nانصراف: بازگشت به برنامه",
                "ذخیره گزارش آبشده‌ها",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (choice == MessageBoxResult.Cancel) return;

            if (choice == MessageBoxResult.Yes)
            {
                try
                {
                    var request = await R4ReadCurrentReportAsync();
                    _reportService.SaveXlsx(_settings.ReportDirectory, request);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        "ذخیره گزارش انجام نشد و برنامه بسته نشد.\n\n" + ex.Message,
                        "خطا در ذخیره گزارش",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                if (Web.CoreWebView2 is not null)
                    await Web.ExecuteScriptAsync("localStorage.removeItem('goldbar.windows.entries.v2');");
            }
            catch { }

            _r4CloseApproved = true;
            Close();
        }
        finally
        {
            _r4ClosingBusy = false;
        }
    }

    private async Task<ReportRequest> R4ReadCurrentReportAsync()
    {
        if (Web.CoreWebView2 is null) return new ReportRequest();
        var encoded = await Web.ExecuteScriptAsync("localStorage.getItem('goldbar.windows.entries.v2') || '[]'");
        var raw = JsonSerializer.Deserialize<string>(encoded) ?? "[]";
        var entries = JsonSerializer.Deserialize<List<ReportEntry>>(raw, _json) ?? [];
        return new ReportRequest
        {
            Entries = entries
                .Where(x => double.IsFinite(x.Weight) && double.IsFinite(x.Assay) && x.Weight > 0 && x.Assay > 0 && x.Assay <= 1000)
                .ToList()
        };
    }

    private void R4Reply(string? id, bool ok, object? data, string? error)
    {
        if (string.IsNullOrWhiteSpace(id) || Web.CoreWebView2 is null) return;
        var json = JsonSerializer.Serialize(new { kind = "r4response", id, ok, data, error }, _json);
        Web.CoreWebView2.PostWebMessageAsJson(json);
    }
}
