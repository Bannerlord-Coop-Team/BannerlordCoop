using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan adds a companion from game interface
    /// </summary>
    public record AddCompanion : IEvent
    {
        public Clan Clan { get; }
        public Hero Companion { get; }

        public AddCompanion(Clan clan, Hero companion)
        {
            Clan = clan;
            Companion = companion;
        }
    }
}