using Common.Messaging;
using GameInterface.Utils.NetworkEvents;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Towns.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkWorkshopsSet : GenericNetworkEvent<Town, Workshop[]>
    {
        [ProtoMember(1)]
        public override string InstanceId { get; set; }

        [ProtoMember(2)]
        public (int index, string id)[] WorkshopIds { get; }

        [ProtoMember(3)]
        public int Length { get; }

        public NetworkWorkshopsSet(string townId, (int index, string id)[] workshopIds, int length) : base(townId)
        {
            WorkshopIds = workshopIds;
            Length = length;
        }
    }
}
