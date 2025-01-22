using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record AlleyListRemoved(Hero Instance, Alley Value) : IEvent
{
    public Hero Instance { get; } = Instance;
    public Alley Value { get; } = Value;
}

