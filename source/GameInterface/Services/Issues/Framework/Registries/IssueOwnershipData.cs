using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Registries;

[ProtoContract(SkipConstructor = true)]
public readonly struct IssueOwnershipData
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly string ControllerId;

    public IssueOwnershipData(string issueOwnerId, string issueId, string controllerId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
    }
}
