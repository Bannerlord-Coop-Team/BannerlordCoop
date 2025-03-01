using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsInProgressSet : GenericEvent<Town, Queue<Building>>
    {
        public BuildingsInProgressSet(Town instance, Queue<Building> value) : base(instance, value)
        {
        }

        public override void HandleEvent(IObjectManager objectManager, INetwork network)
        {
            if (!objectManager.TryGetId(Instance, out string townId)) return;

            network.SendAll(new BuildingsInProgressSetNetwork(townId, null));

            foreach (var item in Value)
            {
                if (!objectManager.TryGetId(Value, out string buildingId)) continue;
                network.SendAll(new BuildingsInProgressEnqueueNetwork(townId, buildingId));
            }
        }
    }
}
