using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan adds renown from game interface
    /// </summary>
    public record AddRenown : IEvent
    {
        public string ClanId { get; }
        public float Amount { get; }
        public bool ShouldNotify { get; }

        public AddRenown(string clanId, float amount, bool shouldNotify)
        {
            ClanId = clanId;
            Amount = amount;
            ShouldNotify = shouldNotify;
        }
    }
}