using Common.Messaging;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan leader is changed
    /// </summary>
    public record ClanLeaderChanged : IEvent
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