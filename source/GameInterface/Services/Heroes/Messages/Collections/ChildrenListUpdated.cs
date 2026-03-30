using Common.Messaging;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record ChildrenListUpdated : GenericEvent<Hero, Hero>
{
    public ChildrenListUpdated(Hero instance, Hero value) : base(instance, value)
    {
    }
}
