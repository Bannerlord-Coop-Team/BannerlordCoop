using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkCaravanPartyOwnerChanged(string CaravanPartyComponentId, string OwnerId) : ICommand
{
    [ProtoMember(1)]
    public string CaravanPartyComponentId { get; } = CaravanPartyComponentId;
    [ProtoMember(2)]
    public string OwnerId { get; } = OwnerId;
}
