using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan adds renown from game interface
    /// </summary>
    public record AddRenown : IEvent
    {
        public Clan Clan { get; }
        public float Amount { get; }
        public bool ShouldNotify { get; }

        public AddRenown(Clan clan, float amount, bool shouldNotify)
        {
            Clan = clan;
            Amount = amount;
            ShouldNotify = shouldNotify;
        }
    }
}