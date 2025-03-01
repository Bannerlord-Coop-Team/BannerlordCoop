using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record WorkshopListUpdated : GenericEvent<Hero, Workshop>
{
    public WorkshopListUpdated(Hero instance, Workshop value) : base(instance, value)
    {
    }

    public override void HandleEvent(IObjectManager objectManager, INetwork network)
    {
        throw new System.NotImplementedException();
    }
}
