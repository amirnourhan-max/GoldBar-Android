using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace GoldBar.Windows;

public sealed class ScaleReader : IDisposable
{
    private readonly object _gate = new();
    private readonly StringBuilder _buffer = new();
    private SerialPort? _port;
    private AppSettings? _settings;
    private TaskCompletionSource<double>? _nextWeight;

    public event Action<double>? WeightReceived;
    public event Action<string>? RawReceived;
    public event Action<string, bool>? StatusChanged;

    public bool IsOpen => _port?.IsOpen == true;
    public double? LastWeight { get; private set; }

    public void ApplySettings(AppSettings settings, bool startIfAuto)
    {
        Stop();
        _settings = settings;
        if (startIfAuto && settings.AutoRead)
        {
            try { Start(); }
            catch (Exception ex) { StatusChanged?.Invoke(ex.Message, false); }
        }
    }

    public void Start()
    {
        if (_settings is null) throw new InvalidOperationException("تنظیمات ترازو بارگذاری نشده است.");
        if (_port?.IsOpen == true) return;

        var p = new SerialPort(
            _settings.PortName,
            _settings.BaudRate,
            _settings.GetParity(),
            _settings.DataBits,
            _settings.GetStopBits())
        {
            Handshake = _settings.GetHandshake(),
            Encoding = Encoding.ASCII,
            ReadTimeout = Math.Max(300, _settings.ReadTimeoutMs),
            WriteTimeout = 1000,
            DtrEnable = false,
            RtsEnable = _settings.GetHandshake() is Handshake.RequestToSend or Handshake.RequestToSendXOnXOff
        };
        p.DataReceived += OnDataReceived;
        p.ErrorReceived += (_, e) => StatusChanged?.Invoke("خطای پورت: " + e.EventType, false);
        p.Open();
        _port = p;
        StatusChanged?.Invoke($"متصل: {_settings.PortName}", true);
    }

    public void Stop()
    {
        lock (_gate)
        {
            try
            {
                if (_port is not null)
                {
                    _port.DataReceived -= OnDataReceived;
                    if (_port.IsOpen) _port.Close();
                    _port.Dispose();
                }
            }
            catch { }
            finally
            {
                _port = null;
                _buffer.Clear();
            }
        }
    }

    public async Task<double> ReadNowAsync(CancellationToken cancellationToken = default)
    {
        if (_settings is null) throw new InvalidOperationException("تنظیمات ترازو مشخص نشده است.");
        if (_port?.IsOpen != true) Start();

        var tcs = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) _nextWeight = tcs;

        if (_settings.SendQueryOnUpArrow && !string.IsNullOrEmpty(_settings.QueryCommand))
        {
            try { _port!.Write(_settings.BuildQuery()); }
            catch (Exception ex) { StatusChanged?.Invoke("ارسال فرمان به ترازو ناموفق بود: " + ex.Message, false); }
        }

        using var timeout = new CancellationTokenSource(Math.Max(500, _settings.ReadTimeoutMs));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        using var reg = linked.Token.Register(() => tcs.TrySetCanceled(linked.Token));

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (LastWeight.HasValue) return LastWeight.Value;
            throw new TimeoutException("در زمان تعیین‌شده وزنی از ترازو دریافت نشد.");
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_nextWeight, tcs)) _nextWeight = null;
            }
        }
    }

    public static bool TryParseWeight(string raw, AppSettings settings, out double weight)
    {
        weight = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var normalized = raw.Trim();
        if (!string.IsNullOrEmpty(settings.DecimalSeparator) && settings.DecimalSeparator != ".")
            normalized = normalized.Replace(settings.DecimalSeparator, ".");
        normalized = normalized.Replace(',', '.');

        var matches = Regex.Matches(normalized, @"[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
        if (matches.Count == 0) return false;

        // A&D frames often contain status text followed by the actual signed weight.
        // Prefer the last numeric token that can be parsed as a finite number.
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var token = matches[i].Value;
            var dot = token.IndexOf('.');
            if (dot >= 0)
            {
                var before = token.TrimStart('+', '-').TakeWhile(c => c != '.').Count();
                var after = token.Length - dot - 1;
                if (settings.CharactersBeforeDecimal > 0 && before > settings.CharactersBeforeDecimal + 4) continue;
                if (settings.CharactersAfterDecimal > 0 && after > settings.CharactersAfterDecimal) continue;
            }

            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                && double.IsFinite(parsed))
            {
                weight = parsed;
                return true;
            }
        }
        return false;
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var p = _port;
            var settings = _settings;
            if (p is null || settings is null || !p.IsOpen) return;

            var text = p.ReadExisting();
            if (string.IsNullOrEmpty(text)) return;
            RawReceived?.Invoke(text);

            lock (_gate)
            {
                _buffer.Append(text);
                var all = _buffer.ToString();
                var parts = Regex.Split(all, "[\\r\\n]+");
                _buffer.Clear();
                if (!all.EndsWith("\r") && !all.EndsWith("\n") && parts.Length > 0)
                    _buffer.Append(parts[^1]);

                var limit = all.EndsWith("\r") || all.EndsWith("\n") ? parts.Length : Math.Max(0, parts.Length - 1);
                for (var i = 0; i < limit; i++)
                {
                    var frame = parts[i].Trim();
                    if (frame.Length == 0) continue;
                    HandleFrame(frame, settings);
                }

                // Some scales send a complete fixed frame without CR/LF. Parse it as well once it is reasonably long.
                if (_buffer.Length >= 8 && TryParseWeight(_buffer.ToString(), settings, out var buffered))
                {
                    PublishWeight(buffered);
                    _buffer.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("خطای دریافت ترازو: " + ex.Message, false);
        }
    }

    private void HandleFrame(string frame, AppSettings settings)
    {
        if (TryParseWeight(frame, settings, out var value)) PublishWeight(value);
    }

    private void PublishWeight(double value)
    {
        LastWeight = value;
        WeightReceived?.Invoke(value);
        _nextWeight?.TrySetResult(value);
    }

    public void Dispose() => Stop();
}
