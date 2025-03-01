using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record ChildrenListRemoved : GenericEvent<Hero, Hero>
{
    public ChildrenListRemoved(Hero instance, Hero value) : base(instance, value)
    {
    }

    public override void HandleEvent(IObjectManager objectManager, INetwork network)
    {
        throw new System.NotImplementedException();
    }
}
