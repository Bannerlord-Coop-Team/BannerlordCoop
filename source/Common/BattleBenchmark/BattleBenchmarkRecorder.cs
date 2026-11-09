using Common.Logging;
using Common.PacketHandlers;
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
/// per-route mission traffic and bytes per message type) and writes the summary as JSON. Idle until started, so the instrumented paths only
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

    /// <summary>Counts one message handed to the reliable channel, before batching.</summary>
    void RecordReliableMessageSent(string route, int bytes);

    /// <summary>
    /// Counts one send handed to LiteNetLib: a movement datagram, or a reliable packet that leaves as one datagram up to
    /// <paramref name="maxSinglePacketBytes"/> and as fragments above it.
    /// </summary>
    void RecordSent(string route, bool reliable, int bytes, int maxSinglePacketBytes, bool relayed);

    /// <summary>Counts one received payload; LiteNetLib hands over a fragmented reliable packet reassembled.</summary>
    void RecordReceived(string route, bool reliable, int bytes);

    void RecordReceivedMessage(Type messageType);

    /// <summary>Counts one message or packet handed to the campaign connection or a mission peer, with its serialized bytes.</summary>
    void RecordTypeSent(Type type, int bytes);

    /// <summary>
    /// Counts one payload as it comes off the campaign connection or a mission peer. An aggregate counts only its framing
    /// here, because <see cref="RecordTypeReceived"/> counts each message inside it as it is unpacked.
    /// </summary>
    void RecordPayloadReceived(object payload, int bytes);

    /// <summary>Counts one received message or packet with its serialized bytes.</summary>
    void RecordTypeReceived(Type type, int bytes);
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
    private static readonly Func<Type, TypeCounter> CreateTypeCounter = _ => new TypeCounter();

    private readonly IBattleBenchmarkProcessSampler processSampler;
    private readonly IBattleBenchmarkMissionProbe missionProbe;
    private readonly IJsonFileIO fileIO;
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
    private ConcurrentDictionary<Type, TypeCounter> sentTypes = new ConcurrentDictionary<Type, TypeCounter>();
    private ConcurrentDictionary<Type, TypeCounter> receivedTypes = new ConcurrentDictionary<Type, TypeCounter>();
    private string lastReportPath;
    private string lastError;

    public BattleBenchmarkRecorder(
        IBattleBenchmarkProcessSampler processSampler,
        IBattleBenchmarkMissionProbe missionProbe,
        IJsonFileIO fileIO)
        : this(
            processSampler,
            missionProbe,
            fileIO,
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
        IJsonFileIO fileIO,
        Func<long> timestamp,
        Action<IGameThreadFrameObserver> attachFrameObserver,
        Action<IGameThreadFrameObserver> detachFrameObserver)
    {
        this.processSampler = processSampler;
        this.missionProbe = missionProbe;
        this.fileIO = fileIO;
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
        // "." and ".." would put the report in the output folder or its parent instead of a run folder.
        if (runId == null || !RunIdPattern.IsMatch(runId) || runId == "." || runId == "..")
        {
            error = "The run id must be 1 to 64 letters, digits, dots, dashes or underscores, and not . or ..";
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
            sentTypes = new ConcurrentDictionary<Type, TypeCounter>();
            receivedTypes = new ConcurrentDictionary<Type, TypeCounter>();
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

    // The traffic writers run on the network threads. Each reads the window's route table before checking recording, so
    // a send that races a window change lands in the closing window, never in the next one.
    public void RecordReliableMessageSent(string route, int bytes)
    {
        ConcurrentDictionary<string, RouteCounters> window = routes;
        if (!recording) return;

        window.GetOrAdd(route, CreateRoute).Sent.AddReliableMessage(bytes);
    }

    public void RecordSent(string route, bool reliable, int bytes, int maxSinglePacketBytes, bool relayed)
    {
        ConcurrentDictionary<string, RouteCounters> window = routes;
        if (!recording) return;

        TrafficTotals sent = window.GetOrAdd(route, CreateRoute).Sent;
        if (reliable)
            sent.AddReliable(bytes, CountReliableDatagrams(bytes, maxSinglePacketBytes), relayed);
        else
            sent.AddMovement(bytes);
    }

    public void RecordReceived(string route, bool reliable, int bytes)
    {
        ConcurrentDictionary<string, RouteCounters> window = routes;
        if (!recording) return;

        TrafficTotals received = window.GetOrAdd(route, CreateRoute).Received;
        if (reliable)
            received.AddReliable(bytes, datagrams: 0, relayed: false);
        else
            received.AddMovement(bytes);
    }

    /// <summary>
    /// The datagrams LiteNetLib sends for one reliable payload: one up to the single-packet size, otherwise fragments
    /// that each carry the single-packet size minus <see cref="LiteNetLib.NetConstants.FragmentHeaderSize"/>.
    /// </summary>
    internal static int CountReliableDatagrams(int bytes, int maxSinglePacketBytes)
    {
        if (bytes <= maxSinglePacketBytes || maxSinglePacketBytes <= LiteNetLib.NetConstants.FragmentHeaderSize) return 1;

        int fragmentBytes = maxSinglePacketBytes - LiteNetLib.NetConstants.FragmentHeaderSize;
        return (bytes + fragmentBytes - 1) / fragmentBytes;
    }

    public void RecordReceivedMessage(Type messageType)
    {
        ConcurrentDictionary<Type, StrongBox<long>> window = receivedMessages;
        if (!recording) return;

        Interlocked.Increment(ref window.GetOrAdd(messageType, CreateMessageCount).Value);
    }

    public void RecordTypeSent(Type type, int bytes)
    {
        ConcurrentDictionary<Type, TypeCounter> window = sentTypes;
        if (!recording || type == null) return;

        window.GetOrAdd(type, CreateTypeCounter).Add(bytes);
    }

    public void RecordPayloadReceived(object payload, int bytes)
    {
        if (!recording || payload == null) return;

        if (payload is AggregateMessagePacket aggregate)
        {
            int framing = bytes;
            foreach (byte[] message in aggregate.Messages ?? Array.Empty<byte[]>()) framing -= message?.Length ?? 0;
            RecordTypeReceived(typeof(AggregateMessagePacket), framing);
            return;
        }

        RecordTypeReceived(payload.GetType(), bytes);
    }

    public void RecordTypeReceived(Type type, int bytes)
    {
        ConcurrentDictionary<Type, TypeCounter> window = receivedTypes;
        if (!recording || type == null) return;

        window.GetOrAdd(type, CreateTypeCounter).Add(bytes);
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
        lock (gate)
        {
            if (!recording)
            {
                reportPath = null;
                error = "No benchmark is recording.";
                return false;
            }

            recording = false;
        }

        detachFrameObserver(this);

        // Finish runs inside GameThread.Update, so nothing here may throw into the game loop.
        BattleBenchmarkReport report;
        string path = null;
        try
        {
            report = BuildReport(endTimestamp, endReason, processSampler.Sample(), missionProbe.Capture());
            path = Path.Combine(outputDirectory, runId,
                $"{report.Process.Role}-{SafeFileName(report.Process.ControllerId)}-{report.Process.ProcessId}.json");
            fileIO.WriteToFile(path, report);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "[BattleBenchmark] Could not write the report for {RunId} to {Path}", runId, path);
            lock (gate)
            {
                lastError = $"Could not write the report for {runId}: {exception.Message}";
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

        // A network thread that passed its recording check just before the window closed can still add to the counters,
        // so read each one once and derive every total from those reads.
        List<BattleBenchmarkRouteTraffic> routeReports = routes.Values
            .OrderBy(route => route.Route, StringComparer.Ordinal)
            .Select(route => route.ToReport(seconds))
            .ToList();
        // Keyed by short type name; same-named messages from different namespaces share a row.
        KeyValuePair<string, long>[] messageCounts = receivedMessages
            .Select(entry => new KeyValuePair<string, long>(entry.Key.Name, Interlocked.Read(ref entry.Value.Value)))
            .GroupBy(entry => entry.Key)
            .Select(group => new KeyValuePair<string, long>(group.Key, group.Sum(entry => entry.Value)))
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();
        long totalMessages = messageCounts.Sum(entry => entry.Value);
        // The server is never a mission peer, so its report leaves the mission traffic out; MessageTraffic holds all of it.
        bool server = ModInformation.IsServer;

        return new BattleBenchmarkReport
        {
            RunId = runId,
            Label = label,
            Process = new BattleBenchmarkProcessInfo
            {
                Role = server ? "server" : missionEnd.IsBattleHost ? "battle-host" : "client",
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
                AllocatedBytes = endSample.AllocatedBytes - startSample.AllocatedBytes,
                AllocatedBytesPerSecond = PerSecond(endSample.AllocatedBytes - startSample.AllocatedBytes, seconds),
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
            Traffic = server ? null : RouteCounters.Sum(routeReports, seconds),
            Routes = server ? null : routeReports,
            ReceivedMessages = new BattleBenchmarkReceivedMessages
            {
                Total = totalMessages,
                PerSecond = PerSecond(totalMessages, seconds),
                ByType = messageCounts.ToDictionary(entry => entry.Key, entry => entry.Value),
            },
            MessageTraffic = new BattleBenchmarkMessageTraffic
            {
                Sent = BuildTypeTraffic(sentTypes, seconds),
                Received = BuildTypeTraffic(receivedTypes, seconds),
            },
        };
    }

    // Reads each counter once, keyed by short type name like ReceivedMessages, largest bytes first.
    private static BattleBenchmarkTypeTraffic BuildTypeTraffic(ConcurrentDictionary<Type, TypeCounter> types, double seconds)
    {
        var rows = types
            .Select(entry => (Name: entry.Key.Name, Counts: entry.Value.Read()))
            .GroupBy(row => row.Name)
            .Select(group => (Name: group.Key, Count: group.Sum(row => row.Counts.Count), Bytes: group.Sum(row => row.Counts.Bytes)))
            .OrderByDescending(row => row.Bytes)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToList();

        return new BattleBenchmarkTypeTraffic
        {
            Total = ToByteCounts(rows.Sum(row => row.Count), rows.Sum(row => row.Bytes), seconds),
            ByType = rows.ToDictionary(row => row.Name, row => ToByteCounts(row.Count, row.Bytes, seconds)),
        };
    }

    private static BattleBenchmarkByteCounts ToByteCounts(long count, long bytes, double seconds) => new BattleBenchmarkByteCounts
    {
        Count = count,
        Bytes = bytes,
        CountPerSecond = PerSecond(count, seconds),
        BytesPerSecond = PerSecond(bytes, seconds),
        AverageBytes = count == 0 ? 0d : (double)bytes / count,
    };

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

    /// <summary>One route's sent and received traffic.</summary>
    private sealed class RouteCounters
    {
        public RouteCounters(string route)
        {
            Route = route;
        }

        public string Route { get; }

        public TrafficTotals Sent { get; } = new TrafficTotals();

        public TrafficTotals Received { get; } = new TrafficTotals();

        public BattleBenchmarkRouteTraffic ToReport(double seconds) => new BattleBenchmarkRouteTraffic
        {
            Route = Route,
            Sent = Sent.ToCounts(seconds),
            Received = Received.ToCounts(seconds),
        };

        // Sums the per-route reports already taken, so the total matches the routes it lists.
        public static BattleBenchmarkRouteTraffic Sum(IEnumerable<BattleBenchmarkRouteTraffic> routes, double seconds)
        {
            var sent = new TrafficTotals();
            var received = new TrafficTotals();
            foreach (BattleBenchmarkRouteTraffic route in routes)
            {
                sent.Add(route.Sent);
                received.Add(route.Received);
            }

            return new BattleBenchmarkRouteTraffic
            {
                Route = "all",
                Sent = sent.ToCounts(seconds),
                Received = received.ToCounts(seconds),
            };
        }
    }

    /// <summary>Thread-safe count and bytes of one message or packet type in one direction.</summary>
    // Locked so a write racing the report can't leave a type with a count and no bytes, or bytes and no count.
    private sealed class TypeCounter
    {
        private readonly object gate = new object();
        private long count;
        private long bytes;

        public void Add(int byteCount)
        {
            lock (gate)
            {
                count++;
                bytes += byteCount;
            }
        }

        public (long Count, long Bytes) Read()
        {
            lock (gate)
            {
                return (count, bytes);
            }
        }
    }

    /// <summary>Thread-safe traffic totals for one direction of one route.</summary>
    private sealed class TrafficTotals
    {
        private long movementDatagrams;
        private long movementBytes;
        private long reliableMessages;
        private long reliableMessageBytes;
        private long reliablePackets;
        private long reliableBytes;
        private long reliableDatagrams;
        private long oversizedReliablePackets;
        private long relayedReliablePackets;
        private long relayedReliableBytes;
        private long relayedReliableDatagrams;

        public void AddMovement(long bytes, long datagrams = 1)
        {
            Interlocked.Add(ref movementDatagrams, datagrams);
            Interlocked.Add(ref movementBytes, bytes);
        }

        public void AddReliableMessage(long bytes, long messages = 1)
        {
            Interlocked.Add(ref reliableMessages, messages);
            Interlocked.Add(ref reliableMessageBytes, bytes);
        }

        public void AddReliable(int bytes, int datagrams, bool relayed)
        {
            Interlocked.Increment(ref reliablePackets);
            Interlocked.Add(ref reliableBytes, bytes);
            Interlocked.Add(ref reliableDatagrams, datagrams);
            if (datagrams > 1) Interlocked.Increment(ref oversizedReliablePackets);
            if (!relayed) return;

            Interlocked.Increment(ref relayedReliablePackets);
            Interlocked.Add(ref relayedReliableBytes, bytes);
            Interlocked.Add(ref relayedReliableDatagrams, datagrams);
        }

        // Only for a local total built on one thread from already-read route reports.
        public void Add(BattleBenchmarkTrafficCounts counts)
        {
            AddMovement(counts.MovementBytes, counts.MovementDatagrams);
            AddReliableMessage(counts.ReliableMessageBytes, counts.ReliableMessages);
            reliablePackets += counts.ReliablePackets;
            reliableBytes += counts.ReliableBytes;
            reliableDatagrams += counts.ReliableDatagrams;
            oversizedReliablePackets += counts.OversizedReliablePackets;
            relayedReliablePackets += counts.RelayedReliablePackets;
            relayedReliableBytes += counts.RelayedReliableBytes;
            relayedReliableDatagrams += counts.RelayedReliableDatagrams;
        }

        public BattleBenchmarkTrafficCounts ToCounts(double seconds)
        {
            long movement = Interlocked.Read(ref movementDatagrams);
            long movementTotal = Interlocked.Read(ref movementBytes);
            long messages = Interlocked.Read(ref reliableMessages);
            long packets = Interlocked.Read(ref reliablePackets);
            long reliable = Interlocked.Read(ref reliableBytes);
            long datagrams = Interlocked.Read(ref reliableDatagrams);
            long oversized = Interlocked.Read(ref oversizedReliablePackets);
            long relayed = Interlocked.Read(ref relayedReliableBytes);
            return new BattleBenchmarkTrafficCounts
            {
                MovementDatagrams = movement,
                MovementBytes = movementTotal,
                MovementDatagramsPerSecond = PerSecond(movement, seconds),
                MovementBytesPerSecond = PerSecond(movementTotal, seconds),
                ReliableMessages = messages,
                ReliableMessageBytes = Interlocked.Read(ref reliableMessageBytes),
                ReliableMessagesPerSecond = PerSecond(messages, seconds),
                ReliablePackets = packets,
                ReliableBytes = reliable,
                ReliablePacketsPerSecond = PerSecond(packets, seconds),
                ReliableBytesPerSecond = PerSecond(reliable, seconds),
                ReliableDatagrams = datagrams,
                ReliableDatagramsPerSecond = PerSecond(datagrams, seconds),
                OversizedReliablePackets = oversized,
                OversizedReliablePacketsPerSecond = PerSecond(oversized, seconds),
                DirectReliableBytesPerSecond = PerSecond(reliable - relayed, seconds),
                RelayedReliablePackets = Interlocked.Read(ref relayedReliablePackets),
                RelayedReliableBytes = relayed,
                RelayedReliableBytesPerSecond = PerSecond(relayed, seconds),
                RelayedReliableDatagrams = Interlocked.Read(ref relayedReliableDatagrams),
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
