using GameInterface.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record WorkshopListUpdated : GenericListEvent<Hero, Workshop>
{
    public WorkshopListUpdated(Hero instance, Workshop value) : base(instance, value)
    {

    }
}
