using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when new heir appointed
    /// </summary>
    public record NewHeirAppointed : IEvent
    {
        public string HeirHeroId { get; }
        public string PlayerHeroId { get; }
        public bool IsRetirement { get; }

        public NewHeirAppointed(string heirHeroId, string playerHeroId, bool isRetirement)
        {
            HeirHeroId = heirHeroId;
            PlayerHeroId = playerHeroId;
            IsRetirement = isRetirement;
        }
    }
}