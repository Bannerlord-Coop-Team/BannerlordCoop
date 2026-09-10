using System;

namespace Common.Network;

/// <summary>
/// Shared safety limits for the dedicated server and the in-process client/server configuration.
/// Keeping these values here prevents production from silently using a different join watchdog.
/// </summary>
public static class NetworkJoinLimits
{
    /// <summary>Maximum time to enter the campaign while the held replay remains backlogged.</summary>
    public static readonly TimeSpan CampaignEntryTimeout = TimeSpan.FromMinutes(15);

    /// <summary>Maximum held world-replay packets for one joining peer.</summary>
    public const int MaxReplayPackets = 100_000;

    /// <summary>
    /// Maximum serialized world-replay memory retained for one joining peer. The packet ceiling
    /// remains the primary gameplay guard; this prevents unusually large messages from making that
    /// count an ineffective memory bound.
    /// </summary>
    public const long MaxReplayPendingBytes = 128L * 1024 * 1024;

    /// <summary>Maximum logical messages emitted in one replay batch.</summary>
    public const int MaxReplayBatchPackets = 512;

    /// <summary>Maximum batches awaiting ordered client application, filled one per server frame.</summary>
    public const int MaxReplayInFlightBatches = 4;

    /// <summary>
    /// Target serialized replay payload emitted per application acknowledgement. This fits within
    /// LiteNetLib's reliable window when the existing 1,200-byte aggregation budget is used. One
    /// individually larger message is allowed through alone so replay cannot deadlock.
    /// </summary>
    public const int MaxReplayBatchBytes = 64 * 1024;

    /// <summary>Maximum server game-thread time spent filling one replay batch.</summary>
    public static readonly TimeSpan ReplayBatchSendBudget = TimeSpan.FromMilliseconds(4);

    /// <summary>
    /// Maximum time without application progress during the replay and baseline handshake. Replay work is
    /// frame-budgeted on the client, so slow machines need time to apply it without freezing a frame.
    /// </summary>
    public static readonly TimeSpan ReplayAppliedTimeout = TimeSpan.FromMinutes(5);
}
