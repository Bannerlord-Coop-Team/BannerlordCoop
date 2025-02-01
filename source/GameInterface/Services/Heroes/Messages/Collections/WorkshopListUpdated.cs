using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record WorkshopListUpdated(Hero Instance, Workshop Value) : IEvent
{
    public Hero Instance { get; } = Instance;
    public Workshop Value { get; } = Value;
}
