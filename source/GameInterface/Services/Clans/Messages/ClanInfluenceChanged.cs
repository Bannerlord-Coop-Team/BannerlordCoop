using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan influence is changed from game interface
    /// </summary>
    [DontLogMessage]
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