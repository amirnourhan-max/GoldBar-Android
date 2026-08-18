using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace GoldBar.Windows;

public sealed class ScaleReader : IDisposable
{
    private readonly object _gate = new();
    private readonly StringBuilder _buffer = new();
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly Queue<double> _stableSamples = new();

    private SerialPort? _port;
    private AppSettings? _settings;
    private TaskCompletionSource<double>? _nextWeight;
    private CancellationTokenSource _lifetime = new();

    public event Action<double>? WeightReceived;
    public event Action<string>? RawReceived;
    public event Action<string, bool>? StatusChanged;

    public bool IsOpen => _port?.IsOpen == true;
    public double? LastWeight { get; private set; }
    public double? LastRawWeight { get; private set; }
    public string? LastStartError { get; private set; }

    public void ApplySettings(AppSettings settings, bool startIfAuto)
    {
        Stop();
        _settings = settings;
        LastStartError = null;
        lock (_gate) _stableSamples.Clear();
        if (startIfAuto && settings.AutoRead) _ = StartAsync(_lifetime.Token);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_port?.IsOpen == true) return;
            await Task.Run(StartCore, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LastStartError = ex.Message;
            StatusChanged?.Invoke("ترازو: " + ex.Message, false);
            throw;
        }
        finally { _startGate.Release(); }
    }

    public void Start() => StartCore();

    private void StartCore()
    {
        if (_settings is null) throw new InvalidOperationException("تنظیمات ترازو بارگذاری نشده است.");
        if (_port?.IsOpen == true) return;
        var s = _settings;
        var p = new SerialPort(s.PortName, s.BaudRate, s.GetParity(), s.DataBits, s.GetStopBits())
        {
            Handshake = s.GetHandshake(),
            Encoding = Encoding.ASCII,
            ReadTimeout = Math.Max(250, s.ReadTimeoutMs),
            WriteTimeout = 750,
            DtrEnable = false,
            RtsEnable = s.GetHandshake() is Handshake.RequestToSend or Handshake.RequestToSendXOnXOff,
            ReadBufferSize = 1024
        };
        p.DataReceived += OnDataReceived;
        p.ErrorReceived += OnErrorReceived;
        try
        {
            p.Open();
            lock (_gate) _port = p;
            LastStartError = null;
            StatusChanged?.Invoke($"ترازو: متصل {s.PortName}", true);
        }
        catch
        {
            p.DataReceived -= OnDataReceived;
            p.ErrorReceived -= OnErrorReceived;
            p.Dispose();
            throw;
        }
    }

    public void Stop()
    {
        var old = Interlocked.Exchange(ref _lifetime, new CancellationTokenSource());
        try { old.Cancel(); } catch { }
        old.Dispose();
        lock (_gate)
        {
            try
            {
                if (_port is not null)
                {
                    _port.DataReceived -= OnDataReceived;
                    _port.ErrorReceived -= OnErrorReceived;
                    if (_port.IsOpen) _port.Close();
                    _port.Dispose();
                }
            }
            catch { }
            finally
            {
                _port = null;
                _buffer.Clear();
                _stableSamples.Clear();
                _nextWeight?.TrySetCanceled();
                _nextWeight = null;
            }
        }
    }

    public async Task<double> ReadNowAsync(CancellationToken cancellationToken = default)
    {
        if (_settings is null) throw new InvalidOperationException("تنظیمات ترازو مشخص نشده است.");
        if (_port?.IsOpen != true) await StartAsync(cancellationToken).ConfigureAwait(false);

        var tcs = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) _nextWeight = tcs;
        if (_settings.SendQueryOnUpArrow && !string.IsNullOrEmpty(_settings.QueryCommand))
        {
            try
            {
                var p = _port;
                if (p?.IsOpen == true) await Task.Run(() => p.Write(_settings.BuildQuery()), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) { StatusChanged?.Invoke("ارسال فرمان به ترازو ناموفق بود: " + ex.Message, false); }
        }

        using var timeout = new CancellationTokenSource(Math.Max(350, _settings.ReadTimeoutMs));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        using var reg = linked.Token.Register(() => tcs.TrySetCanceled(linked.Token));
        try
        {
            var value = await tcs.Task.ConfigureAwait(false);
            PublishAccepted(value);
            return value;
        }
        catch (OperationCanceledException)
        {
            if (LastRawWeight.HasValue)
            {
                PublishAccepted(LastRawWeight.Value);
                return LastRawWeight.Value;
            }
            throw new TimeoutException("در زمان تعیین‌شده وزنی از ترازو دریافت نشد.");
        }
        finally
        {
            lock (_gate) if (ReferenceEquals(_nextWeight, tcs)) _nextWeight = null;
        }
    }

    public static bool TryParseWeight(string raw, AppSettings settings, out double weight)
    {
        weight = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var normalized = raw.Trim();
        if (!string.IsNullOrEmpty(settings.DecimalSeparator) && settings.DecimalSeparator != ".") normalized = normalized.Replace(settings.DecimalSeparator, ".");
        normalized = normalized.Replace(',', '.');
        var matches = Regex.Matches(normalized, @"[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
        if (matches.Count == 0) return false;
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
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
            {
                weight = parsed;
                return true;
            }
        }
        return false;
    }

    public static bool IsStableSeries(IEnumerable<double> values, int requiredSamples, double toleranceGrams, out double stableWeight)
    {
        stableWeight = double.NaN;
        var data = values.Where(double.IsFinite).TakeLast(Math.Max(2, requiredSamples)).ToArray();
        if (data.Length < Math.Max(2, requiredSamples)) return false;
        if (data.Max() - data.Min() > Math.Max(0.000001, toleranceGrams)) return false;
        stableWeight = data.Average();
        return true;
    }

    private void OnErrorReceived(object? sender, SerialErrorReceivedEventArgs e) => StatusChanged?.Invoke("خطای پورت: " + e.EventType, false);

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            SerialPort? p; AppSettings? s;
            lock (_gate) { p = _port; s = _settings; }
            if (p is null || s is null || !p.IsOpen) return;
            var text = p.ReadExisting();
            if (string.IsNullOrEmpty(text)) return;
            RawReceived?.Invoke(text);
            var parsed = new List<double>();
            lock (_gate)
            {
                _buffer.Append(text);
                var all = _buffer.ToString();
                var parts = Regex.Split(all, "[\\r\\n]+");
                _buffer.Clear();
                if (!all.EndsWith("\r") && !all.EndsWith("\n") && parts.Length > 0) _buffer.Append(parts[^1]);
                var limit = all.EndsWith("\r") || all.EndsWith("\n") ? parts.Length : Math.Max(0, parts.Length - 1);
                for (var i = 0; i < limit; i++)
                {
                    var frame = parts[i].Trim();
                    if (frame.Length > 0 && TryParseWeight(frame, s, out var value)) parsed.Add(value);
                }
                if (_buffer.Length >= 6 && TryParseWeight(_buffer.ToString(), s, out var buffered)) { parsed.Add(buffered); _buffer.Clear(); }
            }
            foreach (var value in parsed) HandleParsedWeight(value, s);
        }
        catch (Exception ex) { StatusChanged?.Invoke("خطای دریافت ترازو: " + ex.Message, false); }
    }

    private void HandleParsedWeight(double value, AppSettings s)
    {
        LastRawWeight = value;
        TaskCompletionSource<double>? waiter;
        lock (_gate) waiter = _nextWeight;
        waiter?.TrySetResult(value); // ↑ is immediate.
        if (!s.AutoRead) return;
        if (!s.StableAutoReadOnly) { PublishAccepted(value); return; }

        double? accepted = null;
        lock (_gate)
        {
            _stableSamples.Enqueue(value);
            while (_stableSamples.Count > s.StableSampleCount) _stableSamples.Dequeue();
            if (IsStableSeries(_stableSamples, s.StableSampleCount, s.StableToleranceGrams, out var stable)) accepted = stable;
        }
        if (accepted.HasValue) PublishAccepted(accepted.Value);
    }

    private void PublishAccepted(double value)
    {
        LastWeight = value;
        WeightReceived?.Invoke(value);
    }

    public void Dispose()
    {
        Stop();
        _startGate.Dispose();
    }
}
