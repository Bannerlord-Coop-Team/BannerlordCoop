using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record AliveLordsCacheUpdated : GenericEvent<Kingdom, Hero>
{
    public AliveLordsCacheUpdated(Kingdom instance, Hero value) : base(instance, value)
    {
    }
}
