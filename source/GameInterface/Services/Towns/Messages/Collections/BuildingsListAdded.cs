using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsListAdded : GenericEvent<Town, Building>
    {
        public BuildingsListAdded(Town instance, Building value) : base(instance, value)
        {
        }

        public override void HandleEvent(IObjectManager objectManager, INetwork network)
        {
            throw new System.NotImplementedException();
        }
    }
}
