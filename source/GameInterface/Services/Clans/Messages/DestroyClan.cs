using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan is destroyed
    /// </summary>
    public record DestroyClan : ICommand
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