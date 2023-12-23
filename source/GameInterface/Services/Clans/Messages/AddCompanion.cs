using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan adds companion
    /// </summary>
    public record AddCompanion : ICommand
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