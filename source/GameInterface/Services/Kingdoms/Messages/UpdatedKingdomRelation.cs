using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record UpdatedKingdomRelation : IEvent
    {
        public string ClanId { get; }
        public string KingdomId { get; }
        public int ChangeKingdomActionDetail { get; }
        public int awardMultiplier { get; }
        public bool byRebellion { get; }
        public bool showNotification { get; }

        public UpdatedKingdomRelation(string clanId, string kingdomId, int changeKingdomActionDetail, 
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