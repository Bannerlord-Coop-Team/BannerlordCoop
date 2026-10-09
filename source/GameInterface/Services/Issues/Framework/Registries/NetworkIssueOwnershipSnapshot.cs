using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Registries;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueOwnershipSnapshot : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly IssueOwnershipData[] Owners;

    public NetworkIssueOwnershipSnapshot(IssueOwnershipData[] owners)
    {
        Owners = owners;
    }
}
