using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record ArmyListRemoved : GenericEvent<Kingdom, Army>
{
    public ArmyListRemoved(Kingdom instance, Army value) : base(instance, value)
    {
    }
}
