using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record HeroesCacheUpdated : GenericEvent<Kingdom, Hero>
{
    public HeroesCacheUpdated(Kingdom instance, Hero value) : base(instance, value)
    {
    }
}
