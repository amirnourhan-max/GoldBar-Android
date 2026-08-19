using System.IO.Ports;
using System.Text;
using GoldBar.Windows.Core;
using GoldBar.Windows.Models;

namespace GoldBar.Windows.Services;

public sealed class ScaleService : IDisposable
{
    private readonly object _gate = new();
    private readonly StringBuilder _buffer = new();
    private readonly MedianStabilizer _stabilizer = new(3);
    private SerialPort? _port;
    private System.Threading.Timer? _autoTimer;
    private System.Threading.Timer? _parseTimer;
    private ScaleSettings _settings = ScaleSettings.Defaults();
    private bool _disposed;

    public event Action<double, string>? WeightReceived;
    public event Action<bool, string>? StatusChanged;
    public event Action<string>? Error;

    public bool IsConnected => _port?.IsOpen == true;

    public Task<bool> ConnectAsync(ScaleSettings settings)
    {
        Disconnect();
        _settings = settings.Normalize();
        try
        {
            var port = new SerialPort(_settings.Port, _settings.BaudRate,
                ParseParity(_settings.Parity), _settings.DataBits, ParseStopBits(_settings.StopBits))
            {
                Handshake = ParseHandshake(_settings.FlowControl),
                Encoding = Encoding.ASCII,
                ReadTimeout = Math.Clamp(_settings.ReadIntervalMs, 250, 2000),
                WriteTimeout = 1000,
                DtrEnable = false,
                RtsEnable = false,
                NewLine = "\r\n"
            };
            port.DataReceived += OnDataReceived;
            port.ErrorReceived += OnErrorReceived;
            port.Open();
            _port = port;
            _stabilizer.Reset();
            StatusChanged?.Invoke(true, $"متصل به {_settings.Port}");
            ConfigureAutoRead();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(false, ex.Message);
            Error?.Invoke(ex.Message);
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
            if (_port?.IsOpen != true) return Task.FromResult(false);
            if (!string.IsNullOrEmpty(_settings.RequestCommand)) _port.Write(_settings.RequestCommand);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex.Message);
            return Task.FromResult(false);
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
            _parseTimer?.Dispose();
            _parseTimer = new System.Threading.Timer(_ => FlushBuffer(), null, 40, Timeout.Infinite);
        }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
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
        var stable = _stabilizer.Push(parsed.Value);
        WeightReceived?.Invoke(stable, raw);
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e) => Error?.Invoke(e.EventType.ToString());

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
