using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BugReporting.Messages;

/// <summary>
/// Reports archive/upload completion to the client that requested the diagnostic report.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBugReportResult : IServerToClientCommand
{
    [ProtoMember(1)]
    public string RequestId { get; }

    [ProtoMember(2)]
    public string Message { get; }

    public NetworkBugReportResult(string requestId, string message)
    {
        RequestId = requestId;
        Message = message;
    }
}
