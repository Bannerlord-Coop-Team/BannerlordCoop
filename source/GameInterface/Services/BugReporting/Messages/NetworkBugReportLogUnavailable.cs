using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BugReporting.Messages;

internal enum BugReportLogUnavailableReason
{
    Unknown = 0,
    ConsentNotGranted = 1,
    LogUnavailable = 2,
    CaptureFailed = 3,
}

/// <summary>
/// Completes a client's part of a report when no log can be provided.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBugReportLogUnavailable : ICommand
{
    [ProtoMember(1)]
    public string RequestId { get; }

    [ProtoMember(2)]
    public BugReportLogUnavailableReason Reason { get; }

    public NetworkBugReportLogUnavailable(
        string requestId,
        BugReportLogUnavailableReason reason)
    {
        RequestId = requestId;
        Reason = reason;
    }
}
