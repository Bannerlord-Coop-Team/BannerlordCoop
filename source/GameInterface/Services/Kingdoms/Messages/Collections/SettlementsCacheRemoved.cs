using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record SettlementsCacheRemoved : GenericEvent<Kingdom, Settlement>
{
    public SettlementsCacheRemoved(Kingdom instance, Settlement value) : base(instance, value)
    {
    }
}
