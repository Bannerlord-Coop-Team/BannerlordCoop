using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan leader is changed from game interface
    /// </summary>
    public record ClanLeaderChange : IEvent
    {
        public Clan Clan { get; }
        public Hero NewLeader { get; }

        public ClanLeaderChange(Clan clan, Hero newLeader)
        {
            Clan = clan;
            NewLeader = newLeader;
        }
    }
}