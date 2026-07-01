using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkVillagerPartyVillageChanged(string VillagerPartyComponentId, string VillageId) : ICommand
{
    [ProtoMember(1)]
    public string VillagerPartyComponentId { get; } = VillagerPartyComponentId;
    [ProtoMember(2)]
    public string VillageId { get; } = VillageId;
}
