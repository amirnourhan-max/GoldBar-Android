using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using GoldBar.Windows.Core;
using GoldBar.Windows.Models;

namespace GoldBar.Windows.Services;

public sealed record ScaleReading(double Value, string Raw, string Source, long Sequence, DateTimeOffset Timestamp);
public sealed record ScaleTestResult(bool Ok, double? Weight, string Message, string Raw = "", long LatencyMs = 0);

/// <summary>
/// Production serial engine for the workshop scale. SerialPort has one owner,
/// reads are asynchronous, writes are serialized, packets are framed safely,
/// and manual reads always wait for a fresh response rather than stale buffered data.
/// </summary>
public sealed class ScaleService : IDisposable
{
    private const int ReadBufferSize = 256;
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromSeconds(3);

    private sealed record PendingRead(string Source, DateTimeOffset SentAt, TaskCompletionSource<ScaleReading>? Completion);

    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _decoderGate = new();
    private readonly object _pendingGate = new();
    private readonly ScaleFrameDecoder _decoder = new();
    private readonly List<PendingRead> _pending = [];

    private SerialPort? _port;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private CancellationTokenSource? _autoCts;
    private Task? _autoTask;
    private ScaleSettings _settings = ScaleSettings.Defaults();
    private long _sequence;
    private int _idleGeneration;
    private bool _disposed;

    // Compatibility event used by the current WebView host.
    public event Action<double, string>? WeightReceived;
    // Rich event retained for diagnostics/future UI without changing the old bridge contract.
    public event Action<ScaleReading>? ReadingReceived;
    public event Action<bool, string>? StatusChanged;
    public event Action<string>? Error;

    public bool IsConnected => _port?.IsOpen == true;
    public string LastError { get; private set; } = string.Empty;
    public ScaleReading? LatestReading { get; private set; }

    public async Task<bool> ConnectAsync(ScaleSettings settings)
    {
        ThrowIfDisposed();
        var target = settings.Normalize();
        await _stateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_port?.IsOpen == true && SerialConfigEquals(_settings, target))
            {
                _settings = target;
                RestartAutoLoop();
                return true;
            }

            await DisconnectCoreAsync(false).ConfigureAwait(false);
            LastError = string.Empty;
            _settings = target;

            var port = CreatePort(target);
            _port = port; // assign before Open so any Open failure is still disposed by catch cleanup
            port.ErrorReceived += OnErrorReceived;
            port.Open();

            ResetReceiveState();
            _readCts = new CancellationTokenSource();
            _readTask = ReadLoopAsync(port, _readCts.Token);
            RestartAutoLoop();

            StatusChanged?.Invoke(true, $"متصل به {target.ScaleName} روی {target.Port}");
            return true;
        }
        catch (Exception ex)
        {
            LastError = DescribeException(ex, target.Port);
            await DisconnectCoreAsync(false).ConfigureAwait(false);
            StatusChanged?.Invoke(false, LastError);
            Error?.Invoke(LastError);
            return false;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public void ApplySettings(ScaleSettings settings)
    {
        ThrowIfDisposed();
        _settings = settings.Normalize();
        RestartAutoLoop();
    }

    /// <summary>
    /// Operator-triggered read. It clears stale receive bytes and waits for the next
    /// valid scale frame, preventing an old auto-poll response from being captured.
    /// </summary>
    public async Task<bool> RequestWeightAsync()
    {
        var result = await ReadOnceAsync("manual", 1500, clearStale: true).ConfigureAwait(false);
        return result.Ok;
    }

    public async Task<ScaleTestResult> TestAsync(ScaleSettings settings, int timeoutMs = 1500)
    {
        ThrowIfDisposed();
        var target = settings.Normalize();
        if (!SerialPort.GetPortNames().Any(x => string.Equals(x, target.Port, StringComparison.OrdinalIgnoreCase)))
        {
            return new ScaleTestResult(false, null,
                $"پورت {target.Port} در ویندوز پیدا نشد. کابل، تبدیل USB/Serial، درایور و شماره COM را بررسی کنید.");
        }

        if (!IsConnected || !SerialConfigEquals(_settings, target))
        {
            if (!await ConnectAsync(target).ConfigureAwait(false))
                return new ScaleTestResult(false, null,
                    string.IsNullOrWhiteSpace(LastError) ? "اتصال به ترازو ناموفق بود." : LastError);
        }
        else
        {
            _settings = target;
            RestartAutoLoop();
        }

        return await ReadOnceAsync("test", Math.Clamp(timeoutMs, 500, 5000), clearStale: true)
            .ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        if (_disposed && _port is null) return;
        await _stateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(true).ConfigureAwait(false);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public void Disconnect() => DisconnectAsync().GetAwaiter().GetResult();

    private async Task<ScaleTestResult> ReadOnceAsync(string source, int timeoutMs, bool clearStale)
    {
        if (_port?.IsOpen != true)
        {
            LastError = "ترازو متصل نیست. ابتدا پورت COM و تنظیمات ارتباط را بررسی کنید.";
            return new ScaleTestResult(false, null, LastError);
        }

        var completion = new TaskCompletionSource<ScaleReading>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sw = Stopwatch.StartNew();
        try
        {
            if (!await SendRequestAsync(source, completion, clearStale).ConfigureAwait(false))
                return new ScaleTestResult(false, null, LastError);

            var reading = await completion.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs)).ConfigureAwait(false);
            sw.Stop();
            return new ScaleTestResult(
                true,
                reading.Value,
                $"ترازو پاسخ داد: {reading.Value:0.######} g ({sw.ElapsedMilliseconds} ms)",
                reading.Raw,
                sw.ElapsedMilliseconds);
        }
        catch (TimeoutException)
        {
            RemovePending(completion);
            sw.Stop();
            LastError =
                $"اتصال به {_settings.Port} برقرار است اما در {timeoutMs} ms پاسخ معتبر دریافت نشد. " +
                $"تنظیمات: {_settings.BaudRate} baud, {_settings.DataBits} data bits, {_settings.Parity} parity, " +
                $"{_settings.StopBits} stop bits, فرمان «{_settings.RequestCommand}».";
            return new ScaleTestResult(false, null, LastError, "", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            RemovePending(completion);
            return new ScaleTestResult(false, null, "خواندن ترازو لغو شد.");
        }
        catch (Exception ex)
        {
            RemovePending(completion);
            LastError = DescribeException(ex, _settings.Port);
            Error?.Invoke(LastError);
            return new ScaleTestResult(false, null, LastError);
        }
    }

    private async Task<bool> SendRequestAsync(
        string source,
        TaskCompletionSource<ScaleReading>? completion,
        bool clearStale)
    {
        var port = _port;
        if (port?.IsOpen != true)
        {
            LastError = "پورت ترازو باز نیست.";
            return false;
        }

        await _writeGate.WaitAsync().ConfigureAwait(false);
        PendingRead? request = null;
        try
        {
            if (clearStale)
            {
                ClearPending(cancelWaiters: true);
                lock (_decoderGate)
                {
                    _decoder.Reset();
                    Interlocked.Increment(ref _idleGeneration);
                }

                // The driver buffer can contain a late response from a previous auto poll.
                // Clearing it here makes a keyboard/button read genuinely fresh.
                try { port.DiscardInBuffer(); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
            }

            request = new PendingRead(source, DateTimeOffset.UtcNow, completion);
            lock (_pendingGate)
            {
                PurgeExpiredLocked();
                _pending.Add(request);
            }

            var command = _settings.RequestCommand ?? string.Empty;
            if (command.Length > 0)
            {
                var bytes = Encoding.ASCII.GetBytes(command);
                using var writeTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
                await port.BaseStream.WriteAsync(bytes.AsMemory(), writeTimeout.Token).ConfigureAwait(false);
            }
            // Empty command means a streaming scale; the next incoming valid frame
            // completes a manual/test request without transmitting anything.
            return true;
        }
        catch (OperationCanceledException)
        {
            if (request is not null) RemovePending(request);
            LastError = $"ارسال فرمان به {_settings.Port} بیش از حد طول کشید.";
            Error?.Invoke(LastError);
            return false;
        }
        catch (Exception ex)
        {
            if (request is not null) RemovePending(request);
            LastError = DescribeException(ex, _settings.Port);
            Error?.Invoke(LastError);
            return false;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task ReadLoopAsync(SerialPort port, CancellationToken ct)
    {
        var bytes = new byte[ReadBufferSize];
        try
        {
            while (!ct.IsCancellationRequested && ReferenceEquals(_port, port) && port.IsOpen)
            {
                var count = await port.BaseStream.ReadAsync(bytes.AsMemory(), ct).ConfigureAwait(false);
                if (count > 0) ProcessIncomingBytes(bytes.AsSpan(0, count), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested || !port.IsOpen) { }
        catch (InvalidOperationException) when (ct.IsCancellationRequested || !port.IsOpen) { }
        catch (IOException) when (ct.IsCancellationRequested || !port.IsOpen) { }
        catch (Exception ex)
        {
            LastError = DescribeException(ex, _settings.Port);
            Error?.Invoke(LastError);
            StatusChanged?.Invoke(false, LastError);
        }
    }

    private void ProcessIncomingBytes(ReadOnlySpan<byte> bytes, CancellationToken ct)
    {
        IReadOnlyList<string> frames;
        bool partial;
        int generation;
        lock (_decoderGate)
        {
            frames = _decoder.Push(bytes);
            partial = _decoder.HasBufferedData;
            generation = Interlocked.Increment(ref _idleGeneration);
        }

        foreach (var frame in frames) ProcessFrame(frame);
        if (partial)
            _ = FlushIdleAsync(generation, FrameIdleMs(_settings), ct);
    }

    private async Task FlushIdleAsync(int generation, int delayMs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delayMs, ct).ConfigureAwait(false);
            if (generation != Volatile.Read(ref _idleGeneration)) return;

            string? frame;
            lock (_decoderGate)
            {
                if (generation != Volatile.Read(ref _idleGeneration)) return;
                frame = _decoder.FlushIdle();
                if (frame is not null) Interlocked.Increment(ref _idleGeneration);
            }
            if (frame is not null) ProcessFrame(frame);
        }
        catch (OperationCanceledException) { }
    }

    private void ProcessFrame(string raw)
    {
        var value = WeightParser.Parse(raw, _settings.Decimals);
        if (value is null) return;

        PendingRead? request = null;
        lock (_pendingGate)
        {
            PurgeExpiredLocked();
            if (_pending.Count > 0)
            {
                request = _pending[0];
                _pending.RemoveAt(0);
            }
        }

        var reading = new ScaleReading(
            value.Value,
            raw,
            request?.Source ?? "stream",
            Interlocked.Increment(ref _sequence),
            DateTimeOffset.UtcNow);

        LatestReading = reading;
        WeightReceived?.Invoke(reading.Value, reading.Raw);
        ReadingReceived?.Invoke(reading);
        request?.Completion?.TrySetResult(reading);
    }

    private async Task AutoLoopAsync(CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.RequestCommand)) return;

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.ReadIntervalMs));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (_port?.IsOpen != true) return;
                if (HasPending("manual") || HasPending("test") || HasPending("auto")) continue;
                await SendRequestAsync("auto", completion: null, clearStale: false).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            LastError = DescribeException(ex, _settings.Port);
            Error?.Invoke(LastError);
        }
    }

    private void RestartAutoLoop()
    {
        var old = _autoCts;
        _autoCts = null;
        _autoTask = null;
        try { old?.Cancel(); } catch { }
        old?.Dispose();

        if (!_settings.AutoRead || _port?.IsOpen != true || string.IsNullOrEmpty(_settings.RequestCommand)) return;
        _autoCts = new CancellationTokenSource();
        _autoTask = AutoLoopAsync(_autoCts.Token);
    }

    private async Task DisconnectCoreAsync(bool notify)
    {
        var autoCts = _autoCts;
        var autoTask = _autoTask;
        _autoCts = null;
        _autoTask = null;
        try { autoCts?.Cancel(); } catch { }

        var readCts = _readCts;
        var readTask = _readTask;
        _readCts = null;
        _readTask = null;
        try { readCts?.Cancel(); } catch { }

        var port = _port;
        _port = null;
        if (port is not null)
        {
            try { port.ErrorReceived -= OnErrorReceived; } catch { }
            try { if (port.IsOpen) port.Close(); } catch { }
            try { port.Dispose(); } catch { }
        }

        if (autoTask is not null)
            try { await autoTask.WaitAsync(TimeSpan.FromMilliseconds(300)).ConfigureAwait(false); } catch { }
        if (readTask is not null)
            try { await readTask.WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false); } catch { }

        autoCts?.Dispose();
        readCts?.Dispose();
        ResetReceiveState();
        if (notify) StatusChanged?.Invoke(false, "قطع");
    }

    private void ResetReceiveState()
    {
        lock (_decoderGate)
        {
            _decoder.Reset();
            Interlocked.Increment(ref _idleGeneration);
        }
        ClearPending(cancelWaiters: true);
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        LastError = e.EventType switch
        {
            SerialError.Frame => "خطای Frame؛ Baud Rate، Data Bits، Parity و Stop Bits را بررسی کنید.",
            SerialError.Overrun => "خطای Overrun؛ داده‌ها سریع‌تر از ظرفیت دریافت پورت رسیده‌اند.",
            SerialError.RXOver => "بافر دریافت سریال پر شده است؛ کابل/درایور یا سرعت پورت را بررسی کنید.",
            SerialError.RXParity => "خطای Parity؛ تنظیم Parity صحیح نیست یا خط ارتباط نویز دارد.",
            SerialError.TXFull => "بافر ارسال پورت سریال پر است.",
            _ => $"خطای ارتباط سریال: {e.EventType}"
        };
        Error?.Invoke(LastError);
    }

    private bool HasPending(string source)
    {
        lock (_pendingGate)
        {
            PurgeExpiredLocked();
            return _pending.Any(x => x.Source == source);
        }
    }

    private void PurgeExpiredLocked()
    {
        var cutoff = DateTimeOffset.UtcNow - PendingLifetime;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].SentAt >= cutoff) continue;
            _pending[i].Completion?.TrySetException(new TimeoutException("Scale response expired."));
            _pending.RemoveAt(i);
        }
    }

    private void ClearPending(bool cancelWaiters)
    {
        lock (_pendingGate)
        {
            if (cancelWaiters)
            {
                foreach (var x in _pending)
                    x.Completion?.TrySetCanceled();
            }
            _pending.Clear();
        }
    }

    private void RemovePending(TaskCompletionSource<ScaleReading> completion)
    {
        lock (_pendingGate)
            _pending.RemoveAll(x => ReferenceEquals(x.Completion, completion));
    }

    private void RemovePending(PendingRead request)
    {
        lock (_pendingGate)
            _pending.Remove(request);
    }

    private static SerialPort CreatePort(ScaleSettings s) =>
        new(s.Port, s.BaudRate, ParseParity(s.Parity), s.DataBits, ParseStopBits(s.StopBits))
        {
            Handshake = ParseHandshake(s.FlowControl),
            Encoding = Encoding.ASCII,
            ReadTimeout = 1000,
            WriteTimeout = 750,
            NewLine = "\r\n",
            DtrEnable = false,
            ReadBufferSize = 4096,
            WriteBufferSize = 2048
        };

    private static int FrameIdleMs(ScaleSettings s)
    {
        // Dynamic fallback for devices with no CR/LF: six character-times.
        // For the workshop's default 2400 / 7E2 profile this is ~28 ms.
        var parityBits = s.Parity.Equals("None", StringComparison.OrdinalIgnoreCase) ? 0d : 1d;
        var bitsPerCharacter = 1d + s.DataBits + parityBits + s.StopBits;
        var charMs = 1000d * bitsPerCharacter / Math.Max(300, s.BaudRate);
        return Math.Clamp((int)Math.Ceiling(charMs * 6d), 12, 250);
    }

    private static bool SerialConfigEquals(ScaleSettings a, ScaleSettings b) =>
        a.Port.Equals(b.Port, StringComparison.OrdinalIgnoreCase) &&
        a.BaudRate == b.BaudRate &&
        a.DataBits == b.DataBits &&
        a.Parity.Equals(b.Parity, StringComparison.OrdinalIgnoreCase) &&
        Math.Abs(a.StopBits - b.StopBits) < .001 &&
        a.FlowControl.Equals(b.FlowControl, StringComparison.OrdinalIgnoreCase);

    private static string DescribeException(Exception ex, string port) => ex switch
    {
        UnauthorizedAccessException => $"پورت {port} در اختیار برنامه دیگری است یا دسترسی مجاز نیست.",
        IOException => $"ارتباط با {port} قطع یا نامعتبر است. کابل، تبدیل USB/Serial و درایور را بررسی کنید.",
        ArgumentException => $"تنظیمات پورت {port} معتبر نیست. Baud Rate، Data Bits، Parity و Stop Bits را بررسی کنید.",
        ObjectDisposedException => $"ارتباط {port} در حین عملیات بسته شد.",
        InvalidOperationException => $"پورت {port} در وضعیت قابل استفاده نیست. اتصال را قطع و دوباره برقرار کنید.",
        TimeoutException => $"ترازو روی {port} در زمان مقرر پاسخ نداد.",
        _ => $"خطای ترازو: {ex.Message}"
    };

    private static Parity ParseParity(string value) =>
        Enum.TryParse<Parity>(value, true, out var p) ? p : Parity.Even;

    private static StopBits ParseStopBits(double value) => value switch
    {
        1.5 => StopBits.OnePointFive,
        2 => StopBits.Two,
        _ => StopBits.One
    };

    private static Handshake ParseHandshake(string value) => value switch
    {
        "XOnXOff" => Handshake.XOnXOff,
        "RTS/CTS" => Handshake.RequestToSend,
        _ => Handshake.None
    };

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        Disconnect();
        _disposed = true;
        _stateGate.Dispose();
        _writeGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
