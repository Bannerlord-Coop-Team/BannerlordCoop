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
        public Clan Clan { get; }
        public Kingdom NewKingdom { get; }
        public ChangeKingdomAction.ChangeKingdomActionDetail Detail { get; }
        public int AwardMultiplier { get; }
        public bool ByRebellion { get; }
        public bool ShowNotification { get; }

        public ClanKingdomChange(Clan clan, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, int awardMultiplier, bool byRebellion, bool showNotification)
        {
            Clan = clan;
            NewKingdom = newKingdom;
            Detail = detail;
            AwardMultiplier = awardMultiplier;
            ByRebellion = byRebellion;
            ShowNotification = showNotification;
        }
    }
}