using Common.Messaging;
using GameInterface.Utils.NetworkEvents;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Towns.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkWorkshopsChanged : GenericNetworkEvent<Town, Workshop>
    {
        [ProtoMember(1)]
        public override string InstanceId { get; set; }

        [ProtoMember(2)]
        public string WorkshopId { get; set; }

        [ProtoMember(3)]
        public int Index { get; set; }

        public NetworkWorkshopsChanged(string townId, string workshopId, int index) : base(townId)
        {
            WorkshopId = workshopId;
            Index = index;
        }
    }
}
