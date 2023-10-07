using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan influence updated
    /// </summary>
    public record ClanInfluenceChanged : IEvent
    {
        public string ClanId { get; }
        public float Amount { get; }

        public ClanInfluenceChanged(string clanId, float amount)
        {
            ClanId = clanId;
            Amount = amount;
        }
    }
}