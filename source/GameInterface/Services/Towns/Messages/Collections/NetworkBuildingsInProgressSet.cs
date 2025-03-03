using GameInterface.Utils.NetworkEvents;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkBuildingsInProgressSet : GenericNetworkEvent<Town, Queue<Building>>
    {
        [ProtoMember(1)]
        public override string InstanceId { get; set; }

        [ProtoMember(2)]
        public List<string> BuildingIds { get; set; }

        public NetworkBuildingsInProgressSet(string townId, List<string> buildingIds) : base(townId)
        {
            BuildingIds = buildingIds;
        }
    }
}
