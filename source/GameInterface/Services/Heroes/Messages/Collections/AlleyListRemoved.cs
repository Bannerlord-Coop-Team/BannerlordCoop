using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record AlleyListRemoved : GenericEvent<Hero, Alley>
{
    public AlleyListRemoved(Hero instance, Alley value) : base(instance, value)
    {
    }
}


