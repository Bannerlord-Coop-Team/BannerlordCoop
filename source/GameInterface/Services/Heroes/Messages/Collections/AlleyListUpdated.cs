using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record AlleyListUpdated : GenericEvent<Hero, Alley>
{
    public AlleyListUpdated(Hero instance, Alley value) : base(instance, value)
    {
    }

    public override void HandleEvent(IObjectManager objectManager, INetwork network)
    {
        throw new System.NotImplementedException();
    }
}
