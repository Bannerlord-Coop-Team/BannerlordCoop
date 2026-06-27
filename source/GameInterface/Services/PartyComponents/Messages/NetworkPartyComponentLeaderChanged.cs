using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkPartyComponentLeaderChanged(string PartyComponentId, string NewLeaderId) : ICommand
{
    [ProtoMember(1)]
    public string PartyComponentId { get; } = PartyComponentId;
    [ProtoMember(2)]
    public string NewLeaderId { get; } = NewLeaderId;
}
