using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record SettlementsCacheUpdated : GenericEvent<Kingdom, Settlement>
{
    public SettlementsCacheUpdated(Kingdom instance, Settlement value) : base(instance, value)
    {
    }
}
