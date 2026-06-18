using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record FiefsCacheRemoved : GenericEvent<Kingdom, Town>
{
    public FiefsCacheRemoved(Kingdom instance, Town value) : base(instance, value)
    {
    }
}
