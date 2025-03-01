using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record TradeBoundVillagesCacheListAdded : GenericEvent<Town, Village>
    {
        public TradeBoundVillagesCacheListAdded(Town instance, Village value) : base(instance, value)
        {
        }

        public override void HandleEvent(IObjectManager objectManager, INetwork network)
        {
            throw new System.NotImplementedException();
        }
    }
}
