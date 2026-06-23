using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record VillagesCacheRemoved : GenericEvent<Kingdom, Village>
{
    public VillagesCacheRemoved(Kingdom instance, Village value) : base(instance, value)
    {
    }
}
