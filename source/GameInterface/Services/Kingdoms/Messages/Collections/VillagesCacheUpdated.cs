using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record VillagesCacheUpdated : GenericEvent<Kingdom, Village>
{
    public VillagesCacheUpdated(Kingdom instance, Village value) : base(instance, value)
    {
    }
}
