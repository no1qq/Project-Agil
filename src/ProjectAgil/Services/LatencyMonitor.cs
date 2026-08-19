using System.Net.NetworkInformation;
using ProjectAgil.Models;

namespace ProjectAgil.Services;

public interface ILatencyMonitor
{
    event EventHandler<IReadOnlyList<LatencyStats>>? Updated;

    bool IsRunning { get; }

    bool IsPaused { get; }

    bool IsCapturing { get; }

    Task<BenchmarkRun> CaptureAsync(
        PingTarget target,
        int sampleCount,
        int intervalMs,
        int timeoutMs,
        IProgress<int>? progress = null,
        CancellationToken ct = default
    );

    void Start(IEnumerable<PingTarget> targets, int intervalMs, int timeoutMs, int historySize);

    void Stop();

    void Pause();

    void Resume();

    void Reset();

    IReadOnlyList<double> History(string host);

    IReadOnlyList<LatencyStats> Snapshot();

    string ExportCsv();
}

public sealed class LatencyMonitor : ILatencyMonitor, IDisposable
{
    private const double Timeout = -1d;
    private const int MinecraftIntervalFloorMs = 3000;
    private const int MinecraftTimeoutFloorMs = 4000;
    private const int MinecraftBackoffCeilingMs = 60000;
    private const int MaxBackoffStrikes = 5;

    private readonly object _sync = new();
    private readonly Dictionary<string, Buffer> _buffers = new(StringComparer.OrdinalIgnoreCase);

    private volatile bool _paused;
    private volatile bool _capturing;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private int _historySize = 240;

    public event EventHandler<IReadOnlyList<LatencyStats>>? Updated;

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public bool IsPaused => _paused;

    public bool IsCapturing => _capturing;

    public static int EstimateCaptureSeconds(PingTarget target, int sampleCount, int intervalMs) =>
        (int)Math.Ceiling(sampleCount * Spacing(target, intervalMs, 0) / 1000d);

    public async Task<BenchmarkRun> CaptureAsync(
        PingTarget target,
        int sampleCount,
        int intervalMs,
        int timeoutMs,
        IProgress<int>? progress = null,
        CancellationToken ct = default
    )
    {
        var run = new BenchmarkRun
        {
            Host = target.Host,
            Name = target.Name,
            Kind = target.Kind,
        };

        if (sampleCount <= 0 || string.IsNullOrWhiteSpace(target.Host))
        {
            return run;
        }

        _capturing = true;

        try
        {
            await Task.Delay(400, ct).ConfigureAwait(false);

            using var pinger = target.Kind == PingKind.Icmp ? new Ping() : null;
            var spacing = Math.Max(200, intervalMs);
            var strikes = 0;

            for (var index = 0; index < sampleCount; index++)
            {
                ct.ThrowIfCancellationRequested();

                var (_, value) = target.Kind == PingKind.Minecraft
                    ? await MeasureMinecraftAsync(target, timeoutMs, ct).ConfigureAwait(false)
                    : await MeasureAsync(pinger!, target.Host, timeoutMs, ct).ConfigureAwait(false);

                run.Samples.Add(value);
                progress?.Report(index + 1);

                strikes = value <= MinecraftPing.Refused
                    ? Math.Min(strikes + 1, MaxBackoffStrikes)
                    : Math.Max(strikes - 1, 0);

                if (index < sampleCount - 1)
                {
                    await Task.Delay(Spacing(target, spacing, strikes), ct).ConfigureAwait(false);
                }
            }

            return run;
        }
        finally
        {
            _capturing = false;
        }
    }

    public void Start(IEnumerable<PingTarget> targets, int intervalMs, int timeoutMs, int historySize)
    {
        Stop();

        var active = targets.Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Host)).ToArray();
        if (active.Length == 0)
        {
            return;
        }

        lock (_sync)
        {
            _historySize = Math.Max(30, historySize);
            _buffers.Clear();

            foreach (var target in active)
            {
                _buffers[target.Host] = new Buffer(target.Name, target.Host);
            }
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Run(() => LoopAsync(active, Math.Max(100, intervalMs), Math.Max(200, timeoutMs), token), token);
    }

    public void Stop()
    {
        var cts = _cts;
        _cts = null;

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
            cts.Dispose();
        }
        catch
        {
        }

        _loop = null;
    }

    public void Pause() => _paused = true;

    public void Resume() => _paused = false;

    public void Reset()
    {
        lock (_sync)
        {
            foreach (var buffer in _buffers.Values)
            {
                buffer.Clear();
            }
        }
    }

    public IReadOnlyList<double> History(string host)
    {
        lock (_sync)
        {
            return _buffers.TryGetValue(host, out var buffer) ? [.. buffer.Samples] : [];
        }
    }

    public IReadOnlyList<LatencyStats> Snapshot()
    {
        lock (_sync)
        {
            return [.. _buffers.Values.Select(b => b.ToStats())];
        }
    }

    public string ExportCsv()
    {
        var builder = new System.Text.StringBuilder();
        _ = builder.AppendLine("host,name,sample,latency_ms");

        lock (_sync)
        {
            foreach (var buffer in _buffers.Values)
            {
                for (var i = 0; i < buffer.Samples.Count; i++)
                {
                    var value = buffer.Samples[i];
                    var text = value < 0 ? "timeout" : value.ToString("0.###", CultureInfo.InvariantCulture);
                    _ = builder.AppendLine($"{buffer.Host},{buffer.Name},{i},{text}");
                }
            }
        }

        return builder.ToString();
    }

    private async Task LoopAsync(PingTarget[] targets, int intervalMs, int timeoutMs, CancellationToken ct)
    {
        var pingers = targets
            .Where(t => t.Kind == PingKind.Icmp)
            .ToDictionary(t => t.Host, _ => new Ping(), StringComparer.OrdinalIgnoreCase);

        var due = targets.ToDictionary(t => t.Host, _ => DateTime.MinValue, StringComparer.OrdinalIgnoreCase);
        var strikes = targets.ToDictionary(t => t.Host, _ => 0, StringComparer.OrdinalIgnoreCase);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_paused || _capturing)
                {
                    await Task.Delay(250, ct).ConfigureAwait(false);
                    continue;
                }

                var now = DateTime.UtcNow;
                var ready = targets.Where(t => due[t.Host] <= now).ToArray();

                foreach (var target in ready)
                {
                    due[target.Host] = now.AddMilliseconds(Spacing(target, intervalMs, strikes[target.Host]) - 1);
                }

                var round = ready.Select(target =>
                    target.Kind == PingKind.Minecraft
                        ? MeasureMinecraftAsync(target, timeoutMs, ct)
                        : MeasureAsync(pingers[target.Host], target.Host, timeoutMs, ct)
                );

                var results = await Task.WhenAll(round).ConfigureAwait(false);

                lock (_sync)
                {
                    foreach (var (host, value) in results)
                    {
                        if (_buffers.TryGetValue(host, out var buffer))
                        {
                            buffer.Add(value, _historySize);
                        }
                    }
                }

                foreach (var (host, value) in results)
                {
                    strikes[host] = value <= MinecraftPing.Refused
                        ? Math.Min(strikes[host] + 1, MaxBackoffStrikes)
                        : Math.Max(strikes[host] - 1, 0);
                }

                Updated?.Invoke(this, Snapshot());

                await Task.Delay(intervalMs, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            foreach (var pinger in pingers.Values)
            {
                pinger.Dispose();
            }
        }
    }

    private static int Spacing(PingTarget target, int intervalMs, int strikes)
    {
        if (target.Kind != PingKind.Minecraft)
        {
            return intervalMs;
        }

        var baseline = Math.Max(intervalMs, MinecraftIntervalFloorMs);
        var backed = (long)baseline << Math.Min(strikes, MaxBackoffStrikes);

        return (int)Math.Min(backed, MinecraftBackoffCeilingMs);
    }

    private static async Task<(string Host, double Value)> MeasureMinecraftAsync(
        PingTarget target,
        int timeoutMs,
        CancellationToken ct
    )
    {
        var budget = Math.Max(timeoutMs, MinecraftTimeoutFloorMs);
        var value = await MinecraftPing.MeasureAsync(target.Host, target.Port, budget, ct).ConfigureAwait(false);

        return (target.Host, value);
    }

    private static async Task<(string Host, double Value)> MeasureAsync(
        Ping pinger,
        string host,
        int timeoutMs,
        CancellationToken ct
    )
    {
        try
        {
            var reply = await pinger.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            return reply.Status == IPStatus.Success ? (host, reply.RoundtripTime) : (host, Timeout);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return (host, Timeout);
        }
    }

    public void Dispose() => Stop();

    private sealed class Buffer(string name, string host)
    {
        public string Name { get; } = name;

        public string Host { get; } = host;

        public List<double> Samples { get; } = [];

        public int Sent { get; private set; }

        public int Lost { get; private set; }

        public int Refused { get; private set; }

        public void Add(double value, int limit)
        {
            Sent++;

            if (value <= MinecraftPing.Refused)
            {
                Refused++;
            }
            else if (value < 0)
            {
                Lost++;
            }

            Samples.Add(value);

            while (Samples.Count > limit)
            {
                Samples.RemoveAt(0);
            }
        }

        public void Clear()
        {
            Samples.Clear();
            Sent = 0;
            Lost = 0;
            Refused = 0;
        }

        public LatencyStats ToStats()
        {
            var good = Samples.Where(s => s >= 0).ToArray();
            var jitter = 0d;

            if (good.Length > 1)
            {
                var total = 0d;
                for (var i = 1; i < good.Length; i++)
                {
                    total += Math.Abs(good[i] - good[i - 1]);
                }

                jitter = total / (good.Length - 1);
            }

            var last = Samples.Count > 0 ? Samples[^1] : Timeout;
            var answerable = Sent - Refused;

            return new LatencyStats
            {
                Name = Name,
                Host = Host,
                Current = last < 0 ? 0 : last,
                Online = last >= 0,
                Refusing = last <= MinecraftPing.Refused,
                Average = good.Length > 0 ? good.Average() : 0,
                Minimum = good.Length > 0 ? good.Min() : 0,
                Maximum = good.Length > 0 ? good.Max() : 0,
                Jitter = jitter,
                Loss = answerable <= 0 ? 0 : Lost * 100d / answerable,
                Sent = Sent,
                Answered = good.Length,
                Refused = Refused,
            };
        }
    }
}
