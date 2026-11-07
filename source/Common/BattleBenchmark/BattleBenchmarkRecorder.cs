using Common.Logging;
using Common.Serialization;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Common.BattleBenchmark;

/// <summary>
/// Records one fixed battle benchmark window in this process (frame times, hot-path sections, garbage, CPU, memory
/// and per-route mission traffic) and writes the summary as JSON. Idle until started, so the instrumented paths only
/// read <see cref="IsRecording"/>.
/// </summary>
public interface IBattleBenchmarkRecorder
{
    bool IsRecording { get; }

    BattleBenchmarkStatus Status { get; }

    /// <summary>Starts a window of <paramref name="window"/>; the report goes to <c>outputDirectory/runId/</c>.</summary>
    bool TryStart(string runId, string label, TimeSpan window, string outputDirectory, out string error);

    /// <summary>Ends the current window early and writes its report.</summary>
    bool TryStop(out string reportPath, out string error);

    /// <summary>Times <paramref name="section"/> until the returned scope is disposed; does nothing while idle.</summary>
    BattleBenchmarkScope Measure(BattleBenchmarkSection section);

    void RecordSent(string route, BattleBenchmarkTraffic traffic, int bytes);

    void RecordReceived(string route, BattleBenchmarkTraffic traffic, int bytes);

    void RecordReceivedMessage(Type messageType);
}

/// <inheritdoc cref="IBattleBenchmarkRecorder"/>
public class BattleBenchmarkRecorder : IBattleBenchmarkRecorder, IGameThreadFrameObserver, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleBenchmarkRecorder>();

    internal const string DefaultDirectoryName = "CoopBenchmark";
    internal static readonly TimeSpan MinimumWindow = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan MaximumWindow = TimeSpan.FromMinutes(15);

    // Enough frame samples for the whole window at 400 FPS; later frames still count towards the average and maximum.
    private const int FrameSamplesPerSecond = 400;
    private const int MinimumFrameSamples = 1024;

    private static readonly Regex RunIdPattern = new Regex("^[A-Za-z0-9_.-]{1,64}$", RegexOptions.Compiled);
    private static readonly Func<string, RouteCounters> CreateRoute = route => new RouteCounters(route);
    private static readonly Func<Type, StrongBox<long>> CreateMessageCount = _ => new StrongBox<long>();

    private readonly IBattleBenchmarkProcessSampler processSampler;
    private readonly IBattleBenchmarkMissionProbe missionProbe;
    private readonly Func<long> timestamp;
    private readonly Action<IGameThreadFrameObserver> attachFrameObserver;
    private readonly Action<IGameThreadFrameObserver> detachFrameObserver;
    private readonly object gate = new object();

    private volatile bool recording;
    private string runId;
    private string label;
    private string outputDirectory;
    private TimeSpan window;
    private long windowTicks;
    private long startTimestamp;
    private DateTime startedUtc;
    private BattleBenchmarkProcessSample startSample;
    private BattleBenchmarkMissionState missionStart;
    private long[] frameSamples = Array.Empty<long>();
    private int frameSampleCount;
    private int frameCount;
    private long frameTicksTotal;
    private long frameTicksMax;
    private long lastFrameStart;
    private long[] sectionTicks = new long[(int)BattleBenchmarkSection.Count];
    private long[] sectionCalls = new long[(int)BattleBenchmarkSection.Count];
    private long[] sectionMaxTicks = new long[(int)BattleBenchmarkSection.Count];
    private ConcurrentDictionary<string, RouteCounters> routes = new ConcurrentDictionary<string, RouteCounters>();
    private ConcurrentDictionary<Type, StrongBox<long>> receivedMessages = new ConcurrentDictionary<Type, StrongBox<long>>();
    private string lastReportPath;
    private string lastError;

    public BattleBenchmarkRecorder(IBattleBenchmarkProcessSampler processSampler, IBattleBenchmarkMissionProbe missionProbe)
        : this(
            processSampler,
            missionProbe,
            Stopwatch.GetTimestamp,
            observer => GameThread.Instance.FrameObserver = observer,
            observer =>
            {
                if (ReferenceEquals(GameThread.Instance.FrameObserver, observer))
                    GameThread.Instance.FrameObserver = null;
            })
    {
    }

    internal BattleBenchmarkRecorder(
        IBattleBenchmarkProcessSampler processSampler,
        IBattleBenchmarkMissionProbe missionProbe,
        Func<long> timestamp,
        Action<IGameThreadFrameObserver> attachFrameObserver,
        Action<IGameThreadFrameObserver> detachFrameObserver)
    {
        this.processSampler = processSampler;
        this.missionProbe = missionProbe;
        this.timestamp = timestamp;
        this.attachFrameObserver = attachFrameObserver;
        this.detachFrameObserver = detachFrameObserver;
    }

    public bool IsRecording => recording;

    public BattleBenchmarkStatus Status
    {
        get
        {
            lock (gate)
            {
                return new BattleBenchmarkStatus
                {
                    Recording = recording,
                    RunId = runId,
                    ElapsedSeconds = recording ? ToSeconds(timestamp() - startTimestamp) : 0d,
                    RequestedSeconds = window.TotalSeconds,
                    LastReportPath = lastReportPath,
                    LastError = lastError,
                };
            }
        }
    }

    public bool TryStart(string runId, string label, TimeSpan window, string outputDirectory, out string error)
    {
        if (runId == null || !RunIdPattern.IsMatch(runId))
        {
            error = "The run id must be 1 to 64 letters, digits, dots, dashes or underscores.";
            return false;
        }
        if (window < MinimumWindow || window > MaximumWindow)
        {
            error = $"The window must be from {MinimumWindow.TotalSeconds:0} to {MaximumWindow.TotalSeconds:0} seconds.";
            return false;
        }
        if (!string.IsNullOrEmpty(outputDirectory) && !Path.IsPathRooted(outputDirectory))
        {
            error = "The output directory must be an absolute path.";
            return false;
        }

        lock (gate)
        {
            if (recording)
            {
                error = $"Benchmark {this.runId} is already recording.";
                return false;
            }

            this.runId = runId;
            this.label = label;
            this.outputDirectory = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Environment.CurrentDirectory, DefaultDirectoryName)
                : outputDirectory;
            this.window = window;
            windowTicks = (long)(window.TotalSeconds * Stopwatch.Frequency);
            frameSamples = new long[Math.Max(MinimumFrameSamples, (int)(window.TotalSeconds * FrameSamplesPerSecond))];
            frameSampleCount = 0;
            frameCount = 0;
            frameTicksTotal = 0;
            frameTicksMax = 0;
            lastFrameStart = 0;
            sectionTicks = new long[(int)BattleBenchmarkSection.Count];
            sectionCalls = new long[(int)BattleBenchmarkSection.Count];
            sectionMaxTicks = new long[(int)BattleBenchmarkSection.Count];
            routes = new ConcurrentDictionary<string, RouteCounters>();
            receivedMessages = new ConcurrentDictionary<Type, StrongBox<long>>();
            lastReportPath = null;
            lastError = null;

            processSampler.EnableAllocationMonitoring();
            missionStart = missionProbe.Capture();
            startSample = processSampler.Sample();
            startedUtc = DateTime.UtcNow;
            startTimestamp = timestamp();
            recording = true;
        }

        attachFrameObserver(this);
        Logger.Information("[BattleBenchmark] Started {RunId} for {Seconds:0.#} s", runId, window.TotalSeconds);
        error = null;
        return true;
    }

    public bool TryStop(out string reportPath, out string error)
    {
        if (!recording)
        {
            reportPath = null;
            error = "No benchmark is recording.";
            return false;
        }

        return Finish(timestamp(), "stopped", out reportPath, out error);
    }

    public BattleBenchmarkScope Measure(BattleBenchmarkSection section)
    {
        return recording ? new BattleBenchmarkScope(this, section, timestamp()) : default;
    }

    internal void AddSection(BattleBenchmarkSection section, long startedTimestamp)
    {
        if (!recording) return;

        long elapsed = timestamp() - startedTimestamp;
        int index = (int)section;
        Interlocked.Add(ref sectionTicks[index], elapsed);
        Interlocked.Increment(ref sectionCalls[index]);
        RaiseMaximum(ref sectionMaxTicks[index], elapsed);
    }

    public void RecordSent(string route, BattleBenchmarkTraffic traffic, int bytes)
    {
        if (!recording || route == null) return;

        routes.GetOrAdd(route, CreateRoute).Add(sent: true, traffic, bytes);
    }

    public void RecordReceived(string route, BattleBenchmarkTraffic traffic, int bytes)
    {
        if (!recording || route == null) return;

        routes.GetOrAdd(route, CreateRoute).Add(sent: false, traffic, bytes);
    }

    public void RecordReceivedMessage(Type messageType)
    {
        if (!recording || messageType == null) return;

        Interlocked.Increment(ref receivedMessages.GetOrAdd(messageType, CreateMessageCount).Value);
    }

    public void OnFrame(long drainStartedTimestamp, long drainFinishedTimestamp)
    {
        if (!recording) return;

        int drain = (int)BattleBenchmarkSection.GameThreadDrain;
        long drainTicks = drainFinishedTimestamp - drainStartedTimestamp;
        sectionTicks[drain] += drainTicks;
        sectionCalls[drain]++;
        RaiseMaximum(ref sectionMaxTicks[drain], drainTicks);

        if (lastFrameStart != 0)
        {
            long frameTicks = drainStartedTimestamp - lastFrameStart;
            frameCount++;
            frameTicksTotal += frameTicks;
            if (frameTicks > frameTicksMax) frameTicksMax = frameTicks;
            if (frameSampleCount < frameSamples.Length) frameSamples[frameSampleCount++] = frameTicks;
        }
        lastFrameStart = drainStartedTimestamp;

        if (drainFinishedTimestamp - startTimestamp >= windowTicks)
            Finish(drainFinishedTimestamp, "elapsed", out _, out _);
    }

    private bool Finish(long endTimestamp, string endReason, out string reportPath, out string error)
    {
        BattleBenchmarkReport report;
        string path;
        lock (gate)
        {
            if (!recording)
            {
                reportPath = null;
                error = "No benchmark is recording.";
                return false;
            }

            recording = false;
            report = BuildReport(endTimestamp, endReason, processSampler.Sample(), missionProbe.Capture());
            path = Path.Combine(outputDirectory, runId,
                $"{report.Process.Role}-{SafeFileName(report.Process.ControllerId)}-{report.Process.ProcessId}.json");
        }

        detachFrameObserver(this);

        try
        {
            new JsonFileIO().WriteToFile(path, report);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "[BattleBenchmark] Could not write {Path}", path);
            lock (gate)
            {
                lastError = $"Could not write {path}: {exception.Message}";
            }
            reportPath = null;
            error = lastError;
            return false;
        }

        lock (gate)
        {
            lastReportPath = path;
        }
        Logger.Information("[BattleBenchmark] Wrote {RunId} ({Reason}) to {Path}", report.RunId, endReason, path);
        reportPath = path;
        error = null;
        return true;
    }

    /// <summary>Drops an unfinished window without writing it, so a disposed session leaves no frame observer behind.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            recording = false;
        }
        detachFrameObserver(this);
    }

    internal BattleBenchmarkReport BuildReport(
        long endTimestamp,
        string endReason,
        BattleBenchmarkProcessSample endSample,
        BattleBenchmarkMissionState missionEnd)
    {
        double seconds = ToSeconds(endTimestamp - startTimestamp);
        missionEnd ??= BattleBenchmarkMissionState.None;
        missionStart ??= BattleBenchmarkMissionState.None;

        List<BattleBenchmarkRouteTraffic> routeReports = routes.Values
            .OrderBy(route => route.Route, StringComparer.Ordinal)
            .Select(route => route.ToReport(seconds))
            .ToList();
        long totalMessages = receivedMessages.Values.Sum(count => count.Value);

        return new BattleBenchmarkReport
        {
            RunId = runId,
            Label = label,
            Process = new BattleBenchmarkProcessInfo
            {
                Role = ModInformation.IsServer ? "server" : missionEnd.IsBattleHost ? "battle-host" : "client",
                ControllerId = missionEnd.ControllerId,
                ProcessId = GetProcessId(),
                Configuration = BuildConfiguration,
                Version = typeof(BattleBenchmarkRecorder).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                Runtime = RuntimeInformation.FrameworkDescription,
                ProcessorCount = Environment.ProcessorCount,
            },
            Window = new BattleBenchmarkWindow
            {
                RequestedSeconds = window.TotalSeconds,
                MeasuredSeconds = seconds,
                StartedUtc = startedUtc,
                EndedUtc = startedUtc.AddSeconds(seconds),
                EndReason = endReason,
            },
            Frames = BuildFrames(seconds),
            Sections = Enumerable.Range(0, (int)BattleBenchmarkSection.Count).ToDictionary(
                index => ((BattleBenchmarkSection)index).ToString(),
                index => BuildSection(index, seconds)),
            Garbage = new BattleBenchmarkGarbage
            {
                Gen0Collections = endSample.Gen0Collections - startSample.Gen0Collections,
                Gen1Collections = endSample.Gen1Collections - startSample.Gen1Collections,
                Gen2Collections = endSample.Gen2Collections - startSample.Gen2Collections,
                AllocatedBytes = startSample.AllocatedBytes < 0 || endSample.AllocatedBytes < 0
                    ? -1
                    : endSample.AllocatedBytes - startSample.AllocatedBytes,
                AllocatedBytesPerSecond = startSample.AllocatedBytes < 0 || endSample.AllocatedBytes < 0
                    ? -1
                    : PerSecond(endSample.AllocatedBytes - startSample.AllocatedBytes, seconds),
            },
            Cpu = new BattleBenchmarkCpu
            {
                ProcessorSeconds = (endSample.ProcessorTime - startSample.ProcessorTime).TotalSeconds,
                PercentOfOneCore = 100d * PerSecond((endSample.ProcessorTime - startSample.ProcessorTime).TotalSeconds, seconds),
            },
            Memory = new BattleBenchmarkMemory
            {
                WorkingSetStartMb = ToMegabytes(startSample.WorkingSetBytes),
                WorkingSetEndMb = ToMegabytes(endSample.WorkingSetBytes),
                PeakWorkingSetMb = ToMegabytes(endSample.PeakWorkingSetBytes),
                PrivateEndMb = ToMegabytes(endSample.PrivateBytes),
                ManagedHeapEndMb = ToMegabytes(endSample.ManagedHeapBytes),
            },
            MissionStart = missionStart,
            MissionEnd = missionEnd,
            Traffic = RouteCounters.Sum(routeReports, seconds),
            Routes = routeReports,
            ReceivedMessages = new BattleBenchmarkReceivedMessages
            {
                Total = totalMessages,
                PerSecond = PerSecond(totalMessages, seconds),
                ByType = receivedMessages
                    .OrderByDescending(entry => entry.Value.Value)
                    .ThenBy(entry => entry.Key.Name, StringComparer.Ordinal)
                    .ToDictionary(entry => entry.Key.Name, entry => entry.Value.Value),
            },
        };
    }

    private BattleBenchmarkFrames BuildFrames(double seconds)
    {
        long[] sorted = new long[frameSampleCount];
        Array.Copy(frameSamples, sorted, frameSampleCount);
        Array.Sort(sorted);

        return new BattleBenchmarkFrames
        {
            Count = frameCount,
            FramesPerSecond = PerSecond(frameCount, seconds),
            AverageMs = frameCount == 0 ? 0d : ToMilliseconds(frameTicksTotal) / frameCount,
            P50Ms = ToMilliseconds(Percentile(sorted, 0.50)),
            P95Ms = ToMilliseconds(Percentile(sorted, 0.95)),
            P99Ms = ToMilliseconds(Percentile(sorted, 0.99)),
            MaxMs = ToMilliseconds(frameTicksMax),
            UnsampledFrames = frameCount - frameSampleCount,
        };
    }

    private BattleBenchmarkSectionTiming BuildSection(int index, double seconds)
    {
        long calls = Interlocked.Read(ref sectionCalls[index]);
        double totalMs = ToMilliseconds(Interlocked.Read(ref sectionTicks[index]));
        return new BattleBenchmarkSectionTiming
        {
            Calls = calls,
            TotalMs = totalMs,
            MsPerSecond = PerSecond(totalMs, seconds),
            AverageMicroseconds = calls == 0 ? 0d : 1000d * totalMs / calls,
            MaxMs = ToMilliseconds(Interlocked.Read(ref sectionMaxTicks[index])),
        };
    }

    /// <summary>The nearest-rank percentile of an ascending array, 0 when it is empty.</summary>
    internal static long Percentile(long[] ascending, double fraction)
    {
        if (ascending.Length == 0) return 0;

        int rank = (int)Math.Ceiling(fraction * ascending.Length);
        return ascending[Math.Min(ascending.Length, Math.Max(1, rank)) - 1];
    }

    private static void RaiseMaximum(ref long maximum, long value)
    {
        long current = Interlocked.Read(ref maximum);
        while (value > current)
        {
            long previous = Interlocked.CompareExchange(ref maximum, value, current);
            if (previous == current) return;
            current = previous;
        }
    }

    private static string SafeFileName(string value) =>
        string.IsNullOrEmpty(value) ? "none" : Regex.Replace(value, "[^A-Za-z0-9_.-]", "_");

    private static int GetProcessId()
    {
        using var process = Process.GetCurrentProcess();
        return process.Id;
    }

    private static double ToSeconds(long ticks) => (double)ticks / Stopwatch.Frequency;

    private static double ToMilliseconds(long ticks) => 1000d * ticks / Stopwatch.Frequency;

    private static double ToMegabytes(long bytes) => bytes / (1024d * 1024d);

    private static double PerSecond(double value, double seconds) => seconds <= 0 ? 0d : value / seconds;

#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    /// <summary>Thread-safe counts and bytes for one route, per <see cref="BattleBenchmarkTraffic"/> and direction.</summary>
    private sealed class RouteCounters
    {
        private readonly long[] sentCounts = new long[(int)BattleBenchmarkTraffic.Count];
        private readonly long[] sentBytes = new long[(int)BattleBenchmarkTraffic.Count];
        private readonly long[] receivedCounts = new long[(int)BattleBenchmarkTraffic.Count];
        private readonly long[] receivedBytes = new long[(int)BattleBenchmarkTraffic.Count];

        public RouteCounters(string route)
        {
            Route = route;
        }

        public string Route { get; }

        public void Add(bool sent, BattleBenchmarkTraffic traffic, int bytes)
        {
            int index = (int)traffic;
            Interlocked.Increment(ref (sent ? sentCounts : receivedCounts)[index]);
            Interlocked.Add(ref (sent ? sentBytes : receivedBytes)[index], bytes);
        }

        public BattleBenchmarkRouteTraffic ToReport(double seconds) => new BattleBenchmarkRouteTraffic
        {
            Route = Route,
            Sent = ToCounts(sentCounts, sentBytes, seconds),
            Received = ToCounts(receivedCounts, receivedBytes, seconds),
        };

        public static BattleBenchmarkRouteTraffic Sum(IReadOnlyList<BattleBenchmarkRouteTraffic> routes, double seconds)
        {
            var sent = new long[2 * (int)BattleBenchmarkTraffic.Count];
            var received = new long[2 * (int)BattleBenchmarkTraffic.Count];
            foreach (BattleBenchmarkRouteTraffic route in routes)
            {
                AddTo(sent, route.Sent);
                AddTo(received, route.Received);
            }

            return new BattleBenchmarkRouteTraffic
            {
                Route = "all",
                Sent = FromTotals(sent, seconds),
                Received = FromTotals(received, seconds),
            };
        }

        private static BattleBenchmarkTrafficCounts ToCounts(long[] counts, long[] bytes, double seconds)
        {
            var totals = new long[2 * (int)BattleBenchmarkTraffic.Count];
            for (int index = 0; index < (int)BattleBenchmarkTraffic.Count; index++)
            {
                totals[2 * index] = Interlocked.Read(ref counts[index]);
                totals[(2 * index) + 1] = Interlocked.Read(ref bytes[index]);
            }
            return FromTotals(totals, seconds);
        }

        private static void AddTo(long[] totals, BattleBenchmarkTrafficCounts counts)
        {
            totals[2 * (int)BattleBenchmarkTraffic.Movement] += counts.MovementDatagrams;
            totals[(2 * (int)BattleBenchmarkTraffic.Movement) + 1] += counts.MovementBytes;
            totals[2 * (int)BattleBenchmarkTraffic.ReliableMessage] += counts.ReliableMessages;
            totals[(2 * (int)BattleBenchmarkTraffic.ReliableMessage) + 1] += counts.ReliableMessageBytes;
            totals[2 * (int)BattleBenchmarkTraffic.ReliablePacket] += counts.ReliablePackets;
            totals[(2 * (int)BattleBenchmarkTraffic.ReliablePacket) + 1] += counts.ReliableBytes;
        }

        // totals holds count, bytes pairs indexed by BattleBenchmarkTraffic.
        private static BattleBenchmarkTrafficCounts FromTotals(long[] totals, double seconds)
        {
            long movementDatagrams = totals[2 * (int)BattleBenchmarkTraffic.Movement];
            long movementBytes = totals[(2 * (int)BattleBenchmarkTraffic.Movement) + 1];
            long reliableMessages = totals[2 * (int)BattleBenchmarkTraffic.ReliableMessage];
            long reliableMessageBytes = totals[(2 * (int)BattleBenchmarkTraffic.ReliableMessage) + 1];
            long reliablePackets = totals[2 * (int)BattleBenchmarkTraffic.ReliablePacket];
            long reliableBytes = totals[(2 * (int)BattleBenchmarkTraffic.ReliablePacket) + 1];
            return new BattleBenchmarkTrafficCounts
            {
                MovementDatagrams = movementDatagrams,
                MovementBytes = movementBytes,
                MovementDatagramsPerSecond = PerSecond(movementDatagrams, seconds),
                MovementBytesPerSecond = PerSecond(movementBytes, seconds),
                ReliableMessages = reliableMessages,
                ReliableMessageBytes = reliableMessageBytes,
                ReliableMessagesPerSecond = PerSecond(reliableMessages, seconds),
                ReliablePackets = reliablePackets,
                ReliableBytes = reliableBytes,
                ReliablePacketsPerSecond = PerSecond(reliablePackets, seconds),
                ReliableBytesPerSecond = PerSecond(reliableBytes, seconds),
            };
        }
    }
}

/// <summary>Times one <see cref="BattleBenchmarkSection"/> call; the default value, handed out while idle, does nothing.</summary>
public readonly struct BattleBenchmarkScope : IDisposable
{
    private readonly BattleBenchmarkRecorder recorder;
    private readonly BattleBenchmarkSection section;
    private readonly long startedTimestamp;

    internal BattleBenchmarkScope(BattleBenchmarkRecorder recorder, BattleBenchmarkSection section, long startedTimestamp)
    {
        this.recorder = recorder;
        this.section = section;
        this.startedTimestamp = startedTimestamp;
    }

    public void Dispose()
    {
        recorder?.AddSection(section, startedTimestamp);
    }
}
