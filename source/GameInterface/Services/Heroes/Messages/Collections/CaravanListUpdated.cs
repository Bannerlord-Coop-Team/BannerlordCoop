using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record CaravanListUpdated : GenericEvent<Hero, CaravanPartyComponent>
{
    public CaravanListUpdated(Hero instance, CaravanPartyComponent value) : base(instance, value)
    {
    }

    public override void HandleEvent(IObjectManager objectManager, INetwork network)
    {
        throw new System.NotImplementedException();
    }
}
