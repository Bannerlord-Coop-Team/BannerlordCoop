using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    /// <summary>
    /// Clan name change is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkClanLeaderChangeApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string NewLeaderId { get; }

        public NetworkClanLeaderChangeApproved(string clanId, string newLeaderId)
        {
            ClanId = clanId;
            NewLeaderId = newLeaderId;
        }
    }
}