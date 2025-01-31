using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record CaravanListUpdated(Hero Instance, CaravanPartyComponent Value) : IEvent
{
    public Hero Instance { get; } = Instance;
    public CaravanPartyComponent Value { get; } = Value;
}
