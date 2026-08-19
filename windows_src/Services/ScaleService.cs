using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using GoldBar.Windows.Core;
using GoldBar.Windows.Models;

namespace GoldBar.Windows.Services;

public sealed record ScaleReading(
    double Value,
    string Raw,
    string Source,
    long Sequence,
    DateTimeOffset Timestamp);

public sealed record ScaleTestResult(
    bool Ok,
    double? Weight,
    string Message,
    string Raw = "",
    long LatencyMs = 0);

/// <summary>
/// Owns the complete lifecycle of the workshop scale serial connection.
///
/// Design goals:
/// - one owner for SerialPort and its lifetime;
/// - asynchronous reads through SerialPort.BaseStream;
/// - incremental frame reconstruction (CR/LF, STX/ETX, fragmented packets);
/// - serialized writes so auto/manual commands can never overlap;
/// - manual reads discard stale input and wait for a fresh scale response;
/// - background polling never owns the quick-entry weight field;
/// - deterministic shutdown, reconnect, diagnostics, and error reporting.
/// </summary>
public sealed class ScaleService : IDisposable
{
    private const int ReadBufferSize = 256;
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromSeconds(3);

    private sealed record PendingRead(
        string Source,
        DateTimeOffset SentAt,
        TaskCompletionSource<ScaleReading>? Completion);

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

            await DisconnectCoreAsync(notify: false).ConfigureAwait(false);
            LastError = string.Empty;
            _settings = target;

            var port = CreateConfiguredPort(target);
            port.ErrorReceived += OnErrorReceived;
            port.Open();

            _port = port;
            lock (_decoderGate) _decoder.Reset();
            ClearPending(cancelWaiters: true);

            _readCts = new CancellationTokenSource();
            _readTask = ReadLoopAsync(port, _readCts.Token);
            RestartAutoLoop();

            StatusChanged?.Invoke(true, $"متصل به {target.ScaleName} روی {target.Port}");
            return true;
        }
        catch (Exception ex)
        {
            LastError = DescribeException(ex, target.Port);
            await DisconnectCoreAsync(notify: false).ConfigureAwait(false);
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
    /// Requests one fresh manual reading and waits for the next valid frame.
    /// The receive buffer is cleared first so an older automatic response cannot
    /// be mistaken for the operator's current keyboard/button request.
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
        var ports = SerialPort.GetPortNames();
        if (!ports.Any(p => string.Equals(p, target.Port, StringComparison.OrdinalIgnoreCase)))
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
            await DisconnectCoreAsync(notify: true).ConfigureAwait(false);
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
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!await SendRequestAsync(source, completion, clearStale).ConfigureAwait(false))
                return new ScaleTestResult(false, null, LastError);

            var reading = await completion.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs)).ConfigureAwait(false);
            stopwatch.Stop();
            return new ScaleTestResult(
                true,
                reading.Value,
                $"ترازو پاسخ داد: {reading.Value:0.######} g ({stopwatch.ElapsedMilliseconds} ms)",
                reading.Raw,
                stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException)
        {
            RemovePending(completion);
            stopwatch.Stop();
            LastError =
                $"اتصال به {_settings.Port} برقرار است اما در {timeoutMs} ms پاسخ معتبر از ترازو دریافت نشد. " +
                $"تنظیمات فعلی: {_settings.BaudRate} baud, {_settings.DataBits} data bits, {_settings.Parity} parity, " +
                $"{_settings.StopBits} stop bits, فرمان «{_settings.RequestCommand}».";
            return new ScaleTestResult(false, null, LastError, "", stopwatch.ElapsedMilliseconds);
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
        PendingRead? pending = null;
        try
        {
            // For a manual/test read, stale bytes and stale auto requests must not win
            // the race against the fresh operator request.
            if (clearStale)
            {
                ClearPending(cancelWaiters: true);
                lock (_decoderGate)
                {
                    _decoder.Reset();
                    Interlocked.Increment(ref _idleGeneration);
                }
                try { port.DiscardInBuffer(); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
            }

            pending = new PendingRead(source, DateTimeOffset.UtcNow, completion);
            lock (_pendingGate)
            {
                PurgeExpiredPendingLocked();
                _pending.Add(pending);
            }

            var command = _settings.RequestCommand ?? string.Empty;
            if (command.Length > 0)
            {
                var bytes = Encoding.ASCII.GetBytes(command);
                await port.BaseStream.WriteAsync(bytes.AsMemory(0, bytes.Length)).ConfigureAwait(false);
                await port.BaseStream.FlushAsync().ConfigureAwait(false);
            }
            // When command is empty the device is treated as a streaming scale;
            // the pending request will be completed by the next valid incoming frame.
            return true;
        }
        catch (Exception ex)
        {
            if (pending is not null) RemovePending(pending);
            LastError = DescribeException(ex, _settings.Port);
            Error?.Invoke(LastError);
            return false;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task ReadLoopAsync(SerialPort port, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReadBufferSize];
        try
        {
            while (!cancellationToken.IsCancellationRequested && ReferenceEquals(_port, port) && port.IsOpen)
            {
                var count = await port.BaseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (count <= 0) continue;
                ProcessIncomingBytes(buffer.AsSpan(0, count), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested || !port.IsOpen) { }
        catch (InvalidOperationException) when (cancellationToken.IsCancellationRequested || !port.IsOpen) { }
        catch (IOException ex) when (cancellationToken.IsCancellationRequested || !port.IsOpen) { _ = ex; }
        catch (Exception ex)
        {
            LastError = DescribeException(ex, _settings.Port);
            Error?.Invoke(LastError);
            StatusChanged?.Invoke(false, LastError);
        }
    }

    private void ProcessIncomingBytes(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> frames;
        bool hasPartial;
        int generation;

        lock (_decoderGate)
        {
            frames = _decoder.Push(bytes);
            hasPartial = _decoder.HasBufferedData;
            generation = Interlocked.Increment(ref _idleGeneration);
        }

        foreach (var frame in frames) ProcessFrame(frame);

        if (hasPartial)
        {
            var idleMs = ComputeFrameIdleMilliseconds(_settings);
            _ = FlushIdleFrameAsync(generation, idleMs, cancellationToken);
        }
    }

    private async Task FlushIdleFrameAsync(int generation, int idleMs, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(idleMs, cancellationToken).ConfigureAwait(false);
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
        var parsed = WeightParser.Parse(raw, _settings.Decimals);
        if (parsed is null) return;

        PendingRead? request = null;
        lock (_pendingGate)
        {
            PurgeExpiredPendingLocked();
            if (_pending.Count > 0)
            {
                request = _pending[0];
                _pending.RemoveAt(0);
            }
        }

        var reading = new ScaleReading(
            parsed.Value,
            raw,
            request?.Source ?? "stream",
            Interlocked.Increment(ref _sequence),
            DateTimeOffset.UtcNow);

        LatestReading = reading;
        ReadingReceived?.Invoke(reading);
        request?.Completion?.TrySetResult(reading);
    }

    private async Task AutoLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A scale with an empty command is assumed to stream on its own.
            if (string.IsNullOrEmpty(_settings.RequestCommand)) return;

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.ReadIntervalMs));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_port?.IsOpen != true) return;
                if (HasLivePending("manual") || HasLivePending("test") || HasLivePending("auto")) continue;
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
        var oldCts = _autoCts;
        _autoCts = null;
        try { oldCts?.Cancel(); } catch { }
        oldCts?.Dispose();
        _autoTask = null;

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
        {
            try { await autoTask.WaitAsync(TimeSpan.FromMilliseconds(300)).ConfigureAwait(false); } catch { }
        }
        if (readTask is not null)
        {
            try { await readTask.WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false); } catch { }
        }

        autoCts?.Dispose();
        readCts?.Dispose();
        lock (_decoderGate)
        {
            _decoder.Reset();
            Interlocked.Increment(ref _idleGeneration);
        }
        ClearPending(cancelWaiters: true);

        if (notify) StatusChanged?.Invoke(false, "قطع");
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        LastError = e.EventType switch
        {
            SerialError.Frame => "خطای Frame از ترازو دریافت شد؛ Baud Rate، Data Bits، Parity و Stop Bits را بررسی کنید.",
            SerialError.Overrun => "خطای Overrun در ارتباط ترازو؛ داده‌ها سریع‌تر از ظرفیت دریافت پورت رسیده‌اند.",
            SerialError.RXOver => "بافر دریافت سریال پر شده است؛ کابل/درایور یا سرعت پورت را بررسی کنید.",
            SerialError.RXParity => "خطای Parity در داده ترازو؛ مقدار Parity تنظیمات صحیح نیست یا خط ارتباط نویز دارد.",
            SerialError.TXFull => "بافر ارسال پورت سریال پر است.",
            _ => $"خطای ارتباط سریال: {e.EventType}"
        };
        Error?.Invoke(LastError);
    }

    private bool HasLivePending(string source)
    {
        lock (_pendingGate)
        {
            PurgeExpiredPendingLocked();
            return _pending.Any(x => string.Equals(x.Source, source, StringComparison.Ordinal));
        }
    }

    private void PurgeExpiredPendingLocked()
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
                foreach (var request in _pending)
                    request.Completion?.TrySetCanceled();
            }
            _pending.Clear();
        }
    }

    private void RemovePending(TaskCompletionSource<ScaleReading> completion)
    {
        lock (_pendingGate)
        {
            _pending.RemoveAll(x => ReferenceEquals(x.Completion, completion));
        }
    }

    private void RemovePending(PendingRead request)
    {
        lock (_pendingGate) _pending.Remove(request);
    }

    private static SerialPort CreateConfiguredPort(ScaleSettings settings)
    {
        return new SerialPort(
            settings.Port,
            settings.BaudRate,
            ParseParity(settings.Parity),
            settings.DataBits,
            ParseStopBits(settings.StopBits))
        {
            Handshake = ParseHandshake(settings.FlowControl),
            Encoding = Encoding.ASCII,
            ReadTimeout = 1000,
            WriteTimeout = 750,
            NewLine = "\r\n",
            DtrEnable = false,
            ReadBufferSize = 4096,
            WriteBufferSize = 2048
        };
    }

    private static int ComputeFrameIdleMilliseconds(ScaleSettings settings)
    {
        // Six character-times is long enough to survive normal USB/serial chunking,
        // while still finalizing an unterminated 2400-baud frame in roughly 25-30 ms.
        var parityBits = string.Equals(settings.Parity, "None", StringComparison.OrdinalIgnoreCase) ? 0d : 1d;
        var bitsPerCharacter = 1d + settings.DataBits + parityBits + settings.StopBits;
        var charMs = 1000d * bitsPerCharacter / Math.Max(300, settings.BaudRate);
        return Math.Clamp((int)Math.Ceiling(charMs * 6d), 12, 250);
    }

    private static bool SerialConfigEquals(ScaleSettings a, ScaleSettings b) =>
        string.Equals(a.Port, b.Port, StringComparison.OrdinalIgnoreCase) &&
        a.BaudRate == b.BaudRate &&
        a.DataBits == b.DataBits &&
        string.Equals(a.Parity, b.Parity, StringComparison.OrdinalIgnoreCase) &&
        Math.Abs(a.StopBits - b.StopBits) < 0.001 &&
        string.Equals(a.FlowControl, b.FlowControl, StringComparison.OrdinalIgnoreCase);

    private static string DescribeException(Exception ex, string port) => ex switch
    {
        UnauthorizedAccessException => $"پورت {port} در اختیار برنامه دیگری است یا دسترسی به آن مجاز نیست. برنامه‌های دیگر متصل به ترازو را ببندید.",
        IOException => $"ارتباط با {port} قطع یا نامعتبر است. کابل، تبدیل USB/Serial و درایور را بررسی کنید.",
        ArgumentException => $"تنظیمات پورت {port} معتبر نیست. Baud Rate، Data Bits، Parity و Stop Bits را بررسی کنید.",
        InvalidOperationException => $"پورت {port} در وضعیت قابل استفاده نیست. اتصال را قطع و دوباره برقرار کنید.",
        TimeoutException => $"ترازو روی {port} در زمان مقرر پاسخ نداد.",
        ObjectDisposedException => $"ارتباط {port} در حین عملیات بسته شد.",
        _ => $"خطای ترازو: {ex.Message}"
    };

    private static Parity ParseParity(string value) =>
        Enum.TryParse<Parity>(value, true, out var parity) ? parity : Parity.Even;

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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

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
