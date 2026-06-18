using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record DeadLordsCacheRemoved : GenericEvent<Kingdom, Hero>
{
    public DeadLordsCacheRemoved(Kingdom instance, Hero value) : base(instance, value)
    {
    }
}
