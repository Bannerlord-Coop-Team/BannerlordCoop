using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan appoints new heir
    /// </summary>
    public record AddNewHeir : ICommand
    {
        public string HeirHeroId { get; }
        public string PlayerHeroId { get; }
        public bool IsRetirement { get; }

        public AddNewHeir(string heirHeroId, string playerHeroId, bool isRetirement)
        {
            HeirHeroId = heirHeroId;
            PlayerHeroId = playerHeroId;
            IsRetirement = isRetirement;
        }
    }
}