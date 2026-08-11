using System;

namespace Common.Logging;

/// <summary>
/// Captures one outbound packet type over an exact monotonic wall-clock window.
/// </summary>
public interface IPacketProfileCapture
{
    bool TryStartCapture(
        string packetName,
        TimeSpan duration,
        Action completion,
        out PacketProfileCaptureSnapshot snapshot,
        out string error);

    bool TryGetCapture(out PacketProfileCaptureSnapshot snapshot, out string error);

    bool TryCancelCapture(out PacketProfileCaptureSnapshot snapshot, out string error);
}

/// <summary>
/// Immutable view of an exact packet-profile capture window.
/// </summary>
public sealed class PacketProfileCaptureSnapshot
{
    public string CaptureId { get; }
    public string State { get; }
    public string PacketName { get; }
    public long PacketsSent { get; }
    public long BytesSent { get; }
    public long WindowDurationMilliseconds { get; }
    public long ElapsedMilliseconds { get; }
    public DateTimeOffset StartedUtc { get; }
    public DateTimeOffset ExpectedCompletedUtc { get; }
    public DateTimeOffset? CompletedUtc { get; }
    public bool Cancelled { get; }

    public PacketProfileCaptureSnapshot(
        string captureId,
        string state,
        string packetName,
        long packetsSent,
        long bytesSent,
        long windowDurationMilliseconds,
        long elapsedMilliseconds,
        DateTimeOffset startedUtc,
        DateTimeOffset expectedCompletedUtc,
        DateTimeOffset? completedUtc,
        bool cancelled)
    {
        CaptureId = captureId;
        State = state;
        PacketName = packetName;
        PacketsSent = packetsSent;
        BytesSent = bytesSent;
        WindowDurationMilliseconds = windowDurationMilliseconds;
        ElapsedMilliseconds = elapsedMilliseconds;
        StartedUtc = startedUtc;
        ExpectedCompletedUtc = expectedCompletedUtc;
        CompletedUtc = completedUtc;
        Cancelled = cancelled;
    }
}
