using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan adds a companion from game interface
    /// </summary>
    public record AddCompanion : IEvent
    {
        public string ClanId { get; }
        public string CompanionId { get; }

        public AddCompanion(string clanId, string companionId)
        {
            ClanId = clanId;
            CompanionId = companionId;
        }
    }
}