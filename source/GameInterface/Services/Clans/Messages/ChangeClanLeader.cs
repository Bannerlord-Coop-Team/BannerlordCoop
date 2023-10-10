using Common.Messaging;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan leader is changed
    /// </summary>
    public record ChangeClanLeader : ICommand
    {
        public string ClanId { get; }
        public string NewLeaderId { get; }

        public ChangeClanLeader(string clanId, string newLeaderId)
        {
            ClanId = clanId;
            NewLeaderId = newLeaderId;
        }
    }
}