using System;
using System.Collections.Generic;

namespace Common.BattleBenchmark;

/// <summary>One process's battle benchmark summary, written as JSON at the end of the window.</summary>
public sealed class BattleBenchmarkReport
{
    public const string CurrentSchema = "coop-battle-benchmark/1";

    public string Schema { get; set; } = CurrentSchema;
    public string RunId { get; set; }
    public string Label { get; set; }
    public BattleBenchmarkProcessInfo Process { get; set; }
    public BattleBenchmarkWindow Window { get; set; }
    public BattleBenchmarkFrames Frames { get; set; }
    public Dictionary<string, BattleBenchmarkSectionTiming> Sections { get; set; }
    public BattleBenchmarkGarbage Garbage { get; set; }
    public BattleBenchmarkCpu Cpu { get; set; }
    public BattleBenchmarkMemory Memory { get; set; }
    public BattleBenchmarkMissionState MissionStart { get; set; }
    public BattleBenchmarkMissionState MissionEnd { get; set; }

    /// <summary>All mission routes added together; null on the server, which is never a mission peer.</summary>
    public BattleBenchmarkRouteTraffic Traffic { get; set; }

    public List<BattleBenchmarkRouteTraffic> Routes { get; set; }
    public BattleBenchmarkReceivedMessages ReceivedMessages { get; set; }

    /// <summary>Every message and packet sent and received, by type, on the campaign connection and the mission peers together.</summary>
    public BattleBenchmarkMessageTraffic MessageTraffic { get; set; }
}

/// <summary>Which process wrote a report, and its build.</summary>
public sealed class BattleBenchmarkProcessInfo
{
    /// <summary><c>server</c>, <c>battle-host</c> or <c>client</c>.</summary>
    public string Role { get; set; }
    public string ControllerId { get; set; }
    public int ProcessId { get; set; }
    public string Configuration { get; set; }
    public string Version { get; set; }
    public string Runtime { get; set; }
    public int ProcessorCount { get; set; }
}

/// <summary>When the measurement window ran and how it ended.</summary>
public sealed class BattleBenchmarkWindow
{
    public double RequestedSeconds { get; set; }
    public double MeasuredSeconds { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime EndedUtc { get; set; }

    /// <summary>Why the window ended: <c>elapsed</c> or <c>stopped</c>.</summary>
    public string EndReason { get; set; }
}

/// <summary>Frame times over the window, from one game-thread frame to the next.</summary>
public sealed class BattleBenchmarkFrames
{
    public int Count { get; set; }
    public double FramesPerSecond { get; set; }
    public double AverageMs { get; set; }
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double MaxMs { get; set; }

    /// <summary>Frames past the sample buffer; they still count towards the average and maximum.</summary>
    public int UnsampledFrames { get; set; }
}

/// <summary>Time spent in one <see cref="BattleBenchmarkSection"/> over the window.</summary>
public sealed class BattleBenchmarkSectionTiming
{
    public long Calls { get; set; }
    public double TotalMs { get; set; }
    public double MsPerSecond { get; set; }
    public double AverageMicroseconds { get; set; }
    public double MaxMs { get; set; }
}

/// <summary>Garbage collections and allocations over the window.</summary>
public sealed class BattleBenchmarkGarbage
{
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }

    public long AllocatedBytes { get; set; }
    public double AllocatedBytesPerSecond { get; set; }
}

/// <summary>Process CPU time over the window.</summary>
public sealed class BattleBenchmarkCpu
{
    public double ProcessorSeconds { get; set; }

    /// <summary>Process CPU time over the window as a share of one core; 200 means two busy cores.</summary>
    public double PercentOfOneCore { get; set; }
}

/// <summary>Process memory at the start and end of the window.</summary>
public sealed class BattleBenchmarkMemory
{
    public double WorkingSetStartMb { get; set; }
    public double WorkingSetEndMb { get; set; }

    /// <summary>The process's peak working set since it started, loading included.</summary>
    public double PeakWorkingSetMb { get; set; }
    public double PrivateEndMb { get; set; }
    public double ManagedHeapEndMb { get; set; }
}

/// <summary>Counts and rates for one route: a peer controller id, whether the traffic went directly or through the server relay.</summary>
public sealed class BattleBenchmarkRouteTraffic
{
    public string Route { get; set; }
    public BattleBenchmarkTrafficCounts Sent { get; set; }
    public BattleBenchmarkTrafficCounts Received { get; set; }
}

/// <summary>
/// Traffic in one direction of a route. Bytes are what was handed to LiteNetLib, before its own headers: the payload on a
/// direct mission peer, the serialized relay packet on the campaign relay. Reliable datagrams count fragments and are
/// only known for sent traffic, because LiteNetLib hands over a received fragmented packet reassembled. Received traffic
/// covers the direct mission connection only: the server forwards a relayed payload on the campaign connection, where
/// it can't be told apart from campaign traffic, so relayed traffic shows in the sender's <c>Relayed*</c> counts.
/// </summary>
public sealed class BattleBenchmarkTrafficCounts
{
    public long MovementDatagrams { get; set; }
    public long MovementBytes { get; set; }
    public double MovementDatagramsPerSecond { get; set; }
    public double MovementBytesPerSecond { get; set; }
    public long ReliableMessages { get; set; }
    public long ReliableMessageBytes { get; set; }
    public double ReliableMessagesPerSecond { get; set; }
    public long ReliablePackets { get; set; }
    public long ReliableBytes { get; set; }
    public double ReliablePacketsPerSecond { get; set; }
    public double ReliableBytesPerSecond { get; set; }

    /// <summary>Datagrams on the wire: one per reliable packet up to the single-packet size, fragments above it.</summary>
    public long ReliableDatagrams { get; set; }
    public double ReliableDatagramsPerSecond { get; set; }

    /// <summary>Reliable packets over the single-packet size, which went out as two or more fragments.</summary>
    public long OversizedReliablePackets { get; set; }
    public double OversizedReliablePacketsPerSecond { get; set; }
    public double DirectReliableBytesPerSecond { get; set; }
    public long RelayedReliablePackets { get; set; }
    public long RelayedReliableBytes { get; set; }
    public double RelayedReliableBytesPerSecond { get; set; }
    public long RelayedReliableDatagrams { get; set; }
}

/// <summary>Every network message this process received over the window, by type.</summary>
public sealed class BattleBenchmarkReceivedMessages
{
    public long Total { get; set; }
    public double PerSecond { get; set; }

    /// <summary>Message type name to count, largest first.</summary>
    public Dictionary<string, long> ByType { get; set; }
}

/// <summary>
/// Message and packet traffic by type. Bytes are the serialized message or packet handed to the connection, before
/// LiteNetLib's headers, once per peer it went to. A message sent inside an aggregate counts its own bytes, and the
/// aggregate's framing counts under <c>AggregateMessagePacket</c>. A message relayed
/// through the server counts under its own type and again inside <c>RelayPacket</c>, and the server counts each payload it
/// forwards as a sent <c>RelayPacket</c>.
/// </summary>
public sealed class BattleBenchmarkMessageTraffic
{
    public BattleBenchmarkTypeTraffic Sent { get; set; }
    public BattleBenchmarkTypeTraffic Received { get; set; }
}

/// <summary>One direction's total and its share per type.</summary>
public sealed class BattleBenchmarkTypeTraffic
{
    public BattleBenchmarkByteCounts Total { get; set; }

    /// <summary>Message or packet type name to its counts, most bytes first.</summary>
    public Dictionary<string, BattleBenchmarkByteCounts> ByType { get; set; }
}

/// <summary>How many messages or packets went one way over the window, and their bytes.</summary>
public sealed class BattleBenchmarkByteCounts
{
    public long Count { get; set; }
    public long Bytes { get; set; }
    public double CountPerSecond { get; set; }
    public double BytesPerSecond { get; set; }
    public double AverageBytes { get; set; }
}

/// <summary>What <see cref="IBattleBenchmarkRecorder.Status"/> reports.</summary>
public sealed class BattleBenchmarkStatus
{
    public bool Recording { get; set; }
    public string RunId { get; set; }
    public double ElapsedSeconds { get; set; }
    public double RequestedSeconds { get; set; }
    public string LastReportPath { get; set; }
    public string LastError { get; set; }
}
