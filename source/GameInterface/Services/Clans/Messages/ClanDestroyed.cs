using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan is destroyed from game interface
    /// </summary>
    public record ClanDestroyed : IEvent
    {
        public string ClanId { get; }
        public int Details { get; }

        public ClanDestroyed(string clanId, int details)
        {
            ClanId = clanId;
            Details = details;
        }
    }
}