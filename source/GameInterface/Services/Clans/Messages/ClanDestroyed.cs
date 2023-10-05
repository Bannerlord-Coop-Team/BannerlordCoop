using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan is destroyed
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