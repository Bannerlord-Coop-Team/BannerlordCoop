using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record ChildrenListRemoved : GenericEvent<Hero, Hero>
{
    public ChildrenListRemoved(Hero instance, Hero value) : base(instance, value)
    {
    }
}
