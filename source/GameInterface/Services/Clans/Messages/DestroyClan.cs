using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan is destroyed from game interface
    /// </summary>
    public record DestroyClan : IEvent
    {
        public Clan Clan { get; }
        public int Details { get; }

        public DestroyClan(Clan clan, int details)
        {
            Clan = clan;
            Details = details;
        }
    }
}