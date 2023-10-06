using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan kingdom is changed from game interface
    /// </summary>
    public record ClanKingdomChange : IEvent
    {
        public string ClanId { get; }
        public string NewKingdomId { get; }
        public int Detail { get; }
        public int AwardMultiplier { get; }
        public bool ByRebellion { get; }
        public bool ShowNotification { get; }

        public ClanKingdomChange(string clanId, string newKingdom, int detail, int awardMultiplier, bool byRebellion, bool showNotification)
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