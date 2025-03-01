using GameInterface.Services.ObjectManager;
using GameInterface.Utils.NetworkEvents;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record BuildingsInProgressSetNetwork : GenericNetworkEvent<Town, Queue<Building>>, IGenericBaseNetworkEvent
    {
        [ProtoMember(1)]
        public override string InstanceId { get; set; }

        public override Queue<Building> Value { get; set; }

        public BuildingsInProgressSetNetwork(string instanceId, Queue<Building> value) : base(instanceId, value)
        {
        }

        public override void HandleEvent(IObjectManager objectManager)
        {
            if(!objectManager.TryGetObject(InstanceId, out Town town)) return;
            town.BuildingsInProgress = new Queue<Building>();
        }
    }
}
