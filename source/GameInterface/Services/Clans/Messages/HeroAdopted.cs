using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface hero is adopted
    /// </summary>
    public record HeroAdopted : IEvent
    {
        public string AdoptedHeroId { get; }
        public string ClanId { get; }
        public string PlayerHeroId { get; }

        public HeroAdopted(string adoptedHeroId, string clanId, string playerHeroId)
        {
            AdoptedHeroId = adoptedHeroId;
            ClanId = clanId;
            PlayerHeroId = playerHeroId;
        }
    }
}