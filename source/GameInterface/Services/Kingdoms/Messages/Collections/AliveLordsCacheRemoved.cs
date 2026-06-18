using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record AliveLordsCacheRemoved : GenericEvent<Kingdom, Hero>
{
    public AliveLordsCacheRemoved(Kingdom instance, Hero value) : base(instance, value)
    {
    }
}
