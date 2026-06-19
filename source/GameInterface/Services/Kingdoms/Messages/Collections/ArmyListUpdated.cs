using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record ArmyListUpdated : GenericEvent<Kingdom, Army>
{
    public ArmyListUpdated(Kingdom instance, Army value) : base(instance, value)
    {
    }
}
