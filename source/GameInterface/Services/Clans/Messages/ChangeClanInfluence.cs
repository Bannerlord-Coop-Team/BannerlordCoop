using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan influence changes from game interface
    /// </summary>
    public record ChangeClanInfluence : IEvent
    {
        public string ClanId { get; }
        public float Amount { get; }

        public ChangeClanInfluence(string clanId, float amount)
        {
            ClanId = clanId;
            Amount = amount;
        }
    }
}