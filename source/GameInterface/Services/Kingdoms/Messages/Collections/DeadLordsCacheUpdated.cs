using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record DeadLordsCacheUpdated : GenericEvent<Kingdom, Hero>
{
    public DeadLordsCacheUpdated(Kingdom instance, Hero value) : base(instance, value)
    {
    }
}
