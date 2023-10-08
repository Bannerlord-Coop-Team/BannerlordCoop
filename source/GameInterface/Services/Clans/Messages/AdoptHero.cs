using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when adopt a hero from game interface
    /// </summary>
    public record AdoptHero : IEvent
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