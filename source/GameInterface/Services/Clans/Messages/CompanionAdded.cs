using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan adds companion from game interface
    /// </summary>
    public record CompanionAdded : IEvent
    {
        public string ClanId { get; }
        public string CompanionId { get; }

        public CompanionAdded(string clanId, string companionId)
        {
            ClanId = clanId;
            CompanionId = companionId;
        }
    }
}