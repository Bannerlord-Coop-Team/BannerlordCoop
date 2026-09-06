using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BugReporting.Messages;

/// <summary>
/// Asks a client to provide its current co-op log for a diagnostic bug report.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestBugReportLogs : IServerToClientCommand
{
    [ProtoMember(1)]
    public string RequestId { get; }

    public NetworkRequestBugReportLogs(string requestId)
    {
        RequestId = requestId;
    }
}
