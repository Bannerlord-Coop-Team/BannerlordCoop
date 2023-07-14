using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Local event when a kingdom relation is updated from game interface
    /// </summary>
    public record UpdateKingdomRelation : ICommand
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