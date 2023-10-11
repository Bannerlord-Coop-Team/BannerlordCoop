using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan adopts hero
    /// </summary>
    public record AdoptHero : ICommand
    {
        public string AdoptedHeroId { get; }
        public string ClanId { get; }
        public string PlayerHeroId { get; }

        public AdoptHero(string adoptedHeroId, string clanId, string playerHeroId)
        {
            AdoptedHeroId = adoptedHeroId;
            ClanId = clanId;
            PlayerHeroId = playerHeroId;
        }
    }
}