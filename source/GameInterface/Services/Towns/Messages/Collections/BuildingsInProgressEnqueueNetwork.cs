using GameInterface.Services.ObjectManager;
using GameInterface.Utils.NetworkEvents;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    [ProtoContract(SkipConstructor=true)]
    internal record BuildingsInProgressEnqueueNetwork : GenericReferenceNetworkEvent<Town, Building>
    {
        [ProtoMember(1)]
        public override string InstanceId { get; set; }

        [ProtoMember(2)]
        public override string ValueId { get; set; }

        public BuildingsInProgressEnqueueNetwork(string instanceId, string valueId) : base(instanceId, valueId)
        {
        }


        public override void HandleEvent(IObjectManager objectManager)
        {
            if (!objectManager.TryGetObject(InstanceId, out Town town)) return;
            if (!objectManager.TryGetObject(ValueId, out Building building)) return;
            town.BuildingsInProgress.Enqueue(building);
        }
    }
}