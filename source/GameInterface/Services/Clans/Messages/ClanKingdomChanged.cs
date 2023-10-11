using Common.Messaging;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan kingdom is changed from game interface
    /// </summary>
    public record ClanKingdomChanged : IEvent
    {
        public string ClanId { get; }
        public string NewKingdomId { get; }
        public int Detail { get; }
        public int AwardMultiplier { get; }
        public bool ByRebellion { get; }
        public bool ShowNotification { get; }

        public ClanKingdomChanged(string clanId, string newKingdom, int detail, int awardMultiplier, bool byRebellion, bool showNotification)
        {
            ClanId = clanId;
            NewKingdomId = newKingdom;
            Detail = detail;
            AwardMultiplier = awardMultiplier;
            ByRebellion = byRebellion;
            ShowNotification = showNotification;
        }
    }
}