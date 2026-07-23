using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record WorkshopListRemoved : GenericEvent<Hero, Workshop>
{
    public WorkshopListRemoved(Hero instance, Workshop value) : base(instance, value)
    {
    }
}

