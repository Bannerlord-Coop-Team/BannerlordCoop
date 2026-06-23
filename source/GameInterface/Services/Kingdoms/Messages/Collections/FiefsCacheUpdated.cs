using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record FiefsCacheUpdated : GenericEvent<Kingdom, Town>
{
    public FiefsCacheUpdated(Kingdom instance, Town value) : base(instance, value)
    {
    }
}
