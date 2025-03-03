using GameInterface.Utils.NetworkEvents;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkTradeBoundVillagesCacheAdded : GenericNetworkReferenceEvent<Town, Village>
    {
        [ProtoMember(1)]
        public override string InstanceId { get; set; }

        [ProtoMember(2)]
        public override string ValueId { get; set; }

        public NetworkTradeBoundVillagesCacheAdded(string townId, string villageId) : base(townId, villageId)
        {
        }
    }
}
