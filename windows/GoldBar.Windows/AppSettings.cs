using System.IO.Ports;
using System.Text.Json;

namespace GoldBar.Windows;

public sealed class AppSettings
{
    public int SettingsVersion { get; set; } = 2;

    public string ReportFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "GoldBar Reports");

    public string ScaleModel { get; set; } = "A&D";
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 2400;
    public int DataBits { get; set; } = 7;
    public string Parity { get; set; } = nameof(System.IO.Ports.Parity.Even);
    public string StopBits { get; set; } = nameof(System.IO.Ports.StopBits.Two);
    public string Handshake { get; set; } = nameof(System.IO.Ports.Handshake.None);

    public string DecimalSeparator { get; set; } = ".";
    public int CharactersBeforeDecimal { get; set; } = 4;
    public int CharactersAfterDecimal { get; set; } = 8;
    public int MinimumAfterDecimal { get; set; } = 2;

    public bool ReceivePrintKey { get; set; } = true;
    public bool AutoRead { get; set; } = false;
    public bool ReadOnUpArrow { get; set; } = true;
    public bool ShowRawText { get; set; } = false;

    // Auto-read filtering. Manual ↑ reads remain immediate.
    public bool StableAutoReadOnly { get; set; } = true;
    public int StableSampleCount { get; set; } = 3;
    public double StableToleranceGrams { get; set; } = 0.02;

    public bool SendQueryOnUpArrow { get; set; } = true;
    public string QueryCommand { get; set; } = "Q";
    public string QueryLineEnding { get; set; } = "CRLF";
    public int ReadTimeoutMs { get; set; } = 1800;

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoldBar",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

            // v1 settings had AutoRead=true by default. On first upgrade disable it so
            // the scale no longer continuously overwrites the weight field.
            if (!json.Contains("\"SettingsVersion\"", StringComparison.Ordinal))
            {
                loaded.SettingsVersion = 2;
                loaded.AutoRead = false;
                loaded.StableAutoReadOnly = true;
                loaded.StableSampleCount = 3;
                loaded.StableToleranceGrams = 0.02;
                try { loaded.Save(); } catch { }
            }

            loaded.StableSampleCount = Math.Clamp(loaded.StableSampleCount, 2, 10);
            loaded.StableToleranceGrams = Math.Clamp(loaded.StableToleranceGrams, 0.001, 5.0);
            return loaded;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        SettingsVersion = 2;
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public Parity GetParity() => Enum.TryParse<System.IO.Ports.Parity>(Parity, out var p) ? p : System.IO.Ports.Parity.Even;
    public StopBits GetStopBits() => Enum.TryParse<System.IO.Ports.StopBits>(StopBits, out var s) ? s : System.IO.Ports.StopBits.Two;
    public Handshake GetHandshake() => Enum.TryParse<System.IO.Ports.Handshake>(Handshake, out var h) ? h : System.IO.Ports.Handshake.None;

    public string BuildQuery()
    {
        var ending = QueryLineEnding switch
        {
            "CR" => "\r",
            "LF" => "\n",
            "CRLF" => "\r\n",
            _ => string.Empty
        };
        return (QueryCommand ?? string.Empty) + ending;
    }
}
