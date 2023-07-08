using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record UpdateKingdomRelation : IEvent
    {
        public Clan Clan { get; }
        public Kingdom Kingdom { get; }
        public int ChangeKingdomActionDetail { get; }
        public int awardMultiplier { get; }
        public bool byRebellion { get; }
        public bool showNotification { get; }

        public UpdateKingdomRelation(Clan clan, Kingdom kingdom, int changeKingdomActionDetail, 
            int awardMultiplier, bool byRebellion, bool showNotification)
        {
            Clan = clan;
            Kingdom = kingdom;
            ChangeKingdomActionDetail = changeKingdomActionDetail;
            this.awardMultiplier = awardMultiplier;
            this.byRebellion = byRebellion;
            this.showNotification = showNotification;
        }
    }
}