using Common.Messaging;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan leader is changed from game interface
    /// </summary>
    public record ClanLeaderChanged : ICommand
    {
        public string ClanId { get; }
        public string NewLeaderId { get; }

        public ClanLeaderChanged(string clanId, string newLeaderId)
        {
            ClanId = clanId;
            NewLeaderId = newLeaderId;
        }
    }
}