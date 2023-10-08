using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when when new heir from game interface
    /// </summary>
    public record LocalNewHeir : IEvent
    {
        public string HeirHeroId { get; }
        public string PlayerHeroId { get; }
        public bool IsRetirement { get; }

        public LocalNewHeir(string heirHeroId, string playerHeroId, bool isRetirement)
        {
            HeirHeroId = heirHeroId;
            PlayerHeroId = playerHeroId;
            IsRetirement = isRetirement;
        }
    }
}