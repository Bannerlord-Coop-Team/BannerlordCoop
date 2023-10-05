using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when renown is added
    /// </summary>
    public record RenownAdded : IEvent
    {
        public string ClanId { get; }
        public float Amount { get; }
        public bool ShouldNotify { get; }

        public RenownAdded(string clanId, float amount, bool shouldNotify)
        {
            ClanId = clanId;
            Amount = amount;
            ShouldNotify = shouldNotify;
        }
    }
}