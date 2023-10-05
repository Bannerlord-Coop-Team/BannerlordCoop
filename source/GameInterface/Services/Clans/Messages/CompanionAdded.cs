using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when companion is added
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