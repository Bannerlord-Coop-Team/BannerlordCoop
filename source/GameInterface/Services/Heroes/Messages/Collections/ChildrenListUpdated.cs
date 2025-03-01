using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record ChildrenListUpdated : GenericEvent<Hero, Hero>
{
    public ChildrenListUpdated(Hero instance, Hero value) : base(instance, value)
    {
    }

    public override void HandleEvent(IObjectManager objectManager, INetwork network)
    {
        throw new System.NotImplementedException();
    }
}
