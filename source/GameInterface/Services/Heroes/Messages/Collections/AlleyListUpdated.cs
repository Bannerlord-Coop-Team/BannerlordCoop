using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record AlleyListUpdated : GenericEvent<Hero, Alley>
{
    public AlleyListUpdated(Hero instance, Alley value) : base(instance, value)
    {
    }
}
