using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record ChildrenListUpdated(Hero Instance, Hero Value) : IEvent
{
    public Hero Instance { get; } = Instance;
    public Hero Value { get; } = Value;
}
