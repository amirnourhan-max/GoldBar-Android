using System.IO;
using System.IO.Ports;
using System.Text;
using GoldBar.Windows.Core;
using GoldBar.Windows.Models;

namespace GoldBar.Windows.Services;

public sealed record ScaleTestResult(bool Ok, double? Weight, string Message, string Raw = "");

public sealed class ScaleService : IDisposable
{
    private readonly object _gate = new();
    private readonly StringBuilder _buffer = new();
    private SerialPort? _port;
    private System.Threading.Timer? _autoTimer;
    private System.Threading.Timer? _parseTimer;
    private ScaleSettings _settings = ScaleSettings.Defaults();
    private bool _disposed;

    public event Action<double, string>? WeightReceived;
    public event Action<bool, string>? StatusChanged;
    public event Action<string>? Error;

    public bool IsConnected => _port?.IsOpen == true;
    public string LastError { get; private set; } = string.Empty;

    public Task<bool> ConnectAsync(ScaleSettings settings)
    {
        Disconnect();
        _settings = settings.Normalize();
        LastError = string.Empty;
        try
        {
            var port = new SerialPort(_settings.Port, _settings.BaudRate,
                ParseParity(_settings.Parity), _settings.DataBits, ParseStopBits(_settings.StopBits))
            {
                Handshake = ParseHandshake(_settings.FlowControl),
                Encoding = Encoding.ASCII,
                ReadTimeout = Math.Clamp(_settings.ReadIntervalMs, 150, 1500),
                WriteTimeout = 750,
                DtrEnable = false,
                RtsEnable = false,
                NewLine = "\r\n"
            };
            port.DataReceived += OnDataReceived;
            port.ErrorReceived += OnErrorReceived;
            port.Open();
            _port = port;
            StatusChanged?.Invoke(true, $"متصل به {_settings.Port}");
            ConfigureAutoRead();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            LastError = DescribeException(ex, _settings.Port);
            StatusChanged?.Invoke(false, LastError);
            Error?.Invoke(LastError);
            return Task.FromResult(false);
        }
    }

    public void ApplySettings(ScaleSettings settings)
    {
        _settings = settings.Normalize();
        ConfigureAutoRead();
    }

    public Task<bool> RequestWeightAsync()
    {
        try
        {
            if (_port?.IsOpen != true)
            {
                LastError = "ترازو متصل نیست. ابتدا پورت COM را بررسی و اتصال را برقرار کنید.";
                return Task.FromResult(false);
            }
            if (!string.IsNullOrEmpty(_settings.RequestCommand))
                _port.Write(_settings.RequestCommand);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            LastError = DescribeException(ex, _settings.Port);
            Error?.Invoke(LastError);
            return Task.FromResult(false);
        }
    }

    public async Task<ScaleTestResult> TestAsync(ScaleSettings settings, int timeoutMs = 1400)
    {
        var target = settings.Normalize();
        var ports = SerialPort.GetPortNames();
        if (!ports.Any(p => string.Equals(p, target.Port, StringComparison.OrdinalIgnoreCase)))
        {
            return new ScaleTestResult(false, null,
                $"پورت {target.Port} در ویندوز پیدا نشد. کابل USB/Serial، درایور ترازو و شماره COM را بررسی کنید.");
        }

        if (!IsConnected || !SerialConfigEquals(_settings, target))
        {
            if (!await ConnectAsync(target))
                return new ScaleTestResult(false, null, string.IsNullOrWhiteSpace(LastError) ? "اتصال به ترازو ناموفق بود." : LastError);
        }
        else
        {
            ApplySettings(target);
        }

        var tcs = new TaskCompletionSource<ScaleTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnWeight(double value, string raw) =>
            tcs.TrySetResult(new ScaleTestResult(true, value, $"ترازو پاسخ داد: {value:0.######} g", raw));
        void OnError(string message) =>
            tcs.TrySetResult(new ScaleTestResult(false, null, message));

        WeightReceived += OnWeight;
        Error += OnError;
        try
        {
            if (!await RequestWeightAsync())
                return new ScaleTestResult(false, null, string.IsNullOrWhiteSpace(LastError) ? "فرمان خواندن وزن ارسال نشد." : LastError);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(Math.Clamp(timeoutMs, 500, 5000)));
            if (completed == tcs.Task) return await tcs.Task;

            return new ScaleTestResult(false, null,
                $"اتصال به {target.Port} برقرار شد اما ترازو در زمان مقرر پاسخی نداد. Baud Rate ({target.BaudRate})، Data Bits ({target.DataBits})، Parity ({target.Parity})، Stop Bits ({target.StopBits}) و فرمان P را بررسی کنید.");
        }
        finally
        {
            WeightReceived -= OnWeight;
            Error -= OnError;
        }
    }

    public void Disconnect()
    {
        _autoTimer?.Dispose(); _autoTimer = null;
        _parseTimer?.Dispose(); _parseTimer = null;
        var port = _port; _port = null;
        if (port is not null)
        {
            try { port.DataReceived -= OnDataReceived; port.ErrorReceived -= OnErrorReceived; }
            catch { }
            try { if (port.IsOpen) port.Close(); }
            catch { }
            port.Dispose();
        }
        lock (_gate) _buffer.Clear();
        StatusChanged?.Invoke(false, "قطع");
    }

    private void ConfigureAutoRead()
    {
        _autoTimer?.Dispose(); _autoTimer = null;
        if (!_settings.AutoRead || _port?.IsOpen != true) return;
        _autoTimer = new System.Threading.Timer(async _ => await RequestWeightAsync(), null,
            _settings.ReadIntervalMs, _settings.ReadIntervalMs);
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (sender is not SerialPort port || !port.IsOpen) return;
            var chunk = port.ReadExisting();
            if (string.IsNullOrEmpty(chunk)) return;
            lock (_gate) _buffer.Append(chunk);

            // Most scales terminate one reading with CR/LF. Parse those immediately.
            // If the device sends a frame without a terminator, a very short debounce
            // allows the remaining bytes to arrive without the old one-reading delay.
            if (chunk.IndexOf('\r') >= 0 || chunk.IndexOf('\n') >= 0)
            {
                _parseTimer?.Dispose();
                _parseTimer = null;
                FlushBuffer();
            }
            else
            {
                _parseTimer?.Dispose();
                _parseTimer = new System.Threading.Timer(_ => FlushBuffer(), null, 12, Timeout.Infinite);
            }
        }
        catch (Exception ex)
        {
            LastError = DescribeException(ex, _settings.Port);
            Error?.Invoke(LastError);
        }
    }

    private void FlushBuffer()
    {
        string raw;
        lock (_gate)
        {
            if (_buffer.Length == 0) return;
            raw = _buffer.ToString();
            _buffer.Clear();
        }
        var parsed = WeightParser.Parse(raw, _settings.Decimals);
        if (parsed is null) return;

        // Use the newest reading directly. The previous 3-sample median caused the UI
        // to remain one weighing behind when the load changed quickly.
        WeightReceived?.Invoke(parsed.Value, raw);
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        LastError = e.EventType switch
        {
            SerialError.Frame => "خطای Frame از ترازو دریافت شد؛ تنظیمات Baud Rate/Parity/Data Bits/Stop Bits را بررسی کنید.",
            SerialError.Overrun => "داده‌های ترازو سریع‌تر از دریافت نرم‌افزار ارسال شده‌اند (Overrun).",
            SerialError.RXOver => "بافر دریافت سریال پر شده است؛ اتصال یا سرعت پورت را بررسی کنید.",
            SerialError.RXParity => "خطای Parity در داده ترازو؛ مقدار Parity تنظیمات را بررسی کنید.",
            SerialError.TXFull => "بافر ارسال پورت سریال پر است.",
            _ => $"خطای ارتباط سریال: {e.EventType}"
        };
        Error?.Invoke(LastError);
    }

    private static bool SerialConfigEquals(ScaleSettings a, ScaleSettings b) =>
        string.Equals(a.Port, b.Port, StringComparison.OrdinalIgnoreCase) &&
        a.BaudRate == b.BaudRate && a.DataBits == b.DataBits &&
        string.Equals(a.Parity, b.Parity, StringComparison.OrdinalIgnoreCase) &&
        Math.Abs(a.StopBits - b.StopBits) < 0.001 &&
        string.Equals(a.FlowControl, b.FlowControl, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.RequestCommand, b.RequestCommand, StringComparison.Ordinal);

    private static string DescribeException(Exception ex, string port) => ex switch
    {
        UnauthorizedAccessException => $"پورت {port} در اختیار برنامه دیگری است یا دسترسی به آن مجاز نیست. برنامه‌های دیگرِ متصل به ترازو را ببندید.",
        IOException => $"ارتباط با {port} قطع یا نامعتبر است. کابل، تبدیل USB/Serial و درایور را بررسی کنید.",
        ArgumentException => $"تنظیمات پورت {port} معتبر نیست. پارامترهای ارتباط ترازو را بررسی کنید.",
        InvalidOperationException => $"پورت {port} در وضعیت قابل استفاده نیست. اتصال را قطع و دوباره برقرار کنید.",
        TimeoutException => $"ترازو روی {port} در زمان مقرر پاسخ نداد.",
        _ => $"خطای ترازو: {ex.Message}"
    };

    private static Parity ParseParity(string value) => Enum.TryParse<Parity>(value, true, out var p) ? p : Parity.Even;
    private static StopBits ParseStopBits(double value) => value switch { 1.5 => StopBits.OnePointFive, 2 => StopBits.Two, _ => StopBits.One };
    private static Handshake ParseHandshake(string value) => value switch
    {
        "XOnXOff" => Handshake.XOnXOff,
        "RTS/CTS" => Handshake.RequestToSend,
        _ => Handshake.None
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
