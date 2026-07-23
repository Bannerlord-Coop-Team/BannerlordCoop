using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkCaravanPartySettlementChanged(string CaravanPartyComponentId, string SettlementId) : ICommand
{
    [ProtoMember(1)]
    public string CaravanPartyComponentId { get; } = CaravanPartyComponentId;
    [ProtoMember(2)]
    public string SettlementId { get; } = SettlementId;
}
