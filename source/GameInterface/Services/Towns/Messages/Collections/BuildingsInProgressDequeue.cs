using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsInProgressDequeue : GenericEvent<Town, Building>
    {
        public BuildingsInProgressDequeue(Town instance, Building value) : base(instance, value)
        {
        }

        public override void HandleEvent(IObjectManager objectManager, INetwork network)
        {
            if (!objectManager.TryGetId(Instance, out string townId)) return;
            if (!objectManager.TryGetId(Value, out string buildingId)) return;

            network.SendAll(new BuildingsInProgressDequeueNetwork(townId, buildingId));
        }
    }
}
