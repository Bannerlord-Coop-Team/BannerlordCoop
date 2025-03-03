using Common.Messaging;
using GameInterface.Utils.NetworkEvents;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkBuildingsInProgressRemoved : GenericNetworkReferenceEvent<Town, Building>
    {
        [ProtoMember(1)]
        public override string InstanceId { get; set; }

        [ProtoMember(2)]
        public override string ValueId { get; set; }

        public NetworkBuildingsInProgressRemoved(string townId, string buildingId) : base(townId, buildingId)
        {
        }
    }
}
