using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan renown is changed from game interface
    /// </summary>
    public record ClanRenownAdded : IEvent
    {
        public string ClanId { get; }
        public float Amount { get; }
        public bool ShouldNotify { get; }

        public ClanRenownAdded(string clanId, float amount, bool shouldNotify)
        {
            ClanId = clanId;
            Amount = amount;
            ShouldNotify = shouldNotify;
        }
    }
}