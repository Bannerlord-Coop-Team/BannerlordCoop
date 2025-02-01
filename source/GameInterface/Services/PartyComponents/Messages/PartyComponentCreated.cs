using Common.Messaging;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;
internal record PartyComponentCreated(PartyComponent Instance, string SettlementId = null) : IEvent
{
    public PartyComponent Instance { get; } = Instance;

    public string SettlementId { get; } = SettlementId;
}
