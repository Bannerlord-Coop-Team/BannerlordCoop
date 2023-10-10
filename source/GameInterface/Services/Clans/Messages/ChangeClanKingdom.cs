using Common.Messaging;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan kingdom is changed
    /// </summary>
    public record ChangeClanKingdom : ICommand
    {
        public string ClanId { get; }
        public string NewKingdomId { get; }
        public int DetailId { get; }
        public int AwardMultiplier { get; }
        public bool ByRebellion { get; }
        public bool ShowNotification { get; }

        public ChangeClanKingdom(string clanId, string newKingdomId, int detailId, int awardMultiplier, bool byRebellion, bool showNotification)
        {
            ClanId = clanId;
            NewKingdomId = newKingdomId;
            DetailId = detailId;
            AwardMultiplier = awardMultiplier;
            ByRebellion = byRebellion;
            ShowNotification = showNotification;
        }
    }
}