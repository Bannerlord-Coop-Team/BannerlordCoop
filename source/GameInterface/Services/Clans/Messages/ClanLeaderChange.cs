using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan leader is changed from game interface
    /// </summary>
    public record ClanLeaderChange : IEvent
    {
        public string ClanId { get; }
        public string NewLeaderId { get; }

        public ClanLeaderChange(string clanId, string newLeaderId)
        {
            ClanId = clanId;
            NewLeaderId = newLeaderId;
        }
    }
}