using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan is destroyed from game interface
    /// </summary>
    public record DestroyClan : IEvent
    {
        public string ClanId { get; }
        public int Details { get; }

        public DestroyClan(string clanId, int details)
        {
            ClanId = clanId;
            Details = details;
        }
    }
}