using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.CreationCapture;

/// <summary>
/// Sent by the server so every client builds the same issue from the captured values.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueTypeName;

    [ProtoMember(3)]
    public readonly string IssueId;

    [ProtoMember(4)]
    public readonly byte[] Captured;

    public NetworkIssueCreated(string issueOwnerId, string issueTypeName, string issueId, byte[] captured)
    {
        IssueOwnerId = issueOwnerId;
        IssueTypeName = issueTypeName;
        IssueId = issueId;
        Captured = captured;
    }
}
