using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Request from client to server to update kingdom relation
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkUpdateKingdomRelationRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string KingdomId { get; }
        [ProtoMember(3)]
        public int ChangeKingdomActionDetail { get; }
        [ProtoMember(4)]
        public int awardMultiplier { get; }
        [ProtoMember(5)]
        public bool byRebellion { get; }
        [ProtoMember(6)]
        public bool showNotification { get; }

        public NetworkUpdateKingdomRelationRequest(string clanId, string kingdomId, int changeKingdomActionDetail, 
            int awardMultiplier, bool byRebellion, bool showNotification)
        {
            ClanId = clanId;
            KingdomId = kingdomId;
            ChangeKingdomActionDetail = changeKingdomActionDetail;
            this.awardMultiplier = awardMultiplier;
            this.byRebellion = byRebellion;
            this.showNotification = showNotification;
        }
    }
}