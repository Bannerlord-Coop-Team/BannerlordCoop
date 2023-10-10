using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan appoints new heir from game interface
    /// </summary>
    public record NewHeirAdded : ICommand
    {
        public string HeirHeroId { get; }
        public string PlayerHeroId { get; }
        public bool IsRetirement { get; }

        public NewHeirAdded(string heirHeroId, string playerHeroId, bool isRetirement)
        {
            HeirHeroId = heirHeroId;
            PlayerHeroId = playerHeroId;
            IsRetirement = isRetirement;
        }
    }
}