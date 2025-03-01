using Common.Messaging;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record ChildrenListUpdated : GenericListEvent<Hero, Hero>
{
    public ChildrenListUpdated(Hero instance, Hero value) : base(instance, value)
    {
    }
}
