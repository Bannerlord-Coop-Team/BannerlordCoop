namespace Common.BattleBenchmark;

/// <summary>A kind of mission traffic the battle benchmark counts per route.</summary>
public enum BattleBenchmarkTraffic
{
    /// <summary>An unreliable datagram; in a mission these carry movement and mount movement.</summary>
    Movement = 0,

    /// <summary>One message handed to the reliable channel, before batching.</summary>
    ReliableMessage = 1,

    /// <summary>One reliable payload put on the wire, a batch of messages or a reliable packet.</summary>
    ReliablePacket = 2,

    Count = 3,
}
