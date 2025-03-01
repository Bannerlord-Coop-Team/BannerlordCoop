using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record TradeBoundVillagesCacheListRemoved : GenericEvent<Town, Village>
    {
        public TradeBoundVillagesCacheListRemoved(Town instance, Village value) : base(instance, value)
        {
        }

        public override void HandleEvent(IObjectManager objectManager, INetwork network)
        {
            throw new System.NotImplementedException();
        }
    }
}
