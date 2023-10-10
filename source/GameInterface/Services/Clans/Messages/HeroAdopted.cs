using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan adopts hero from game interface
    /// </summary>
    public record HeroAdopted : ICommand
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