using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Bandits.Messages
{
    /// <summary>
    /// Local event when a bandit is spawned from game interface
    /// </summary>
    public record BanditsSpawned : IEvent
    {
        public string ClanId { get; }

        public BanditsSpawned(string clanId)
        {
            ClanId = clanId;
        }
    }
}