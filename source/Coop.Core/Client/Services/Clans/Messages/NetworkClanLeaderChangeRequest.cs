using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.Clans.Messages
{
    /// <summary>
    /// Request from client to server to change clan leader
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkClanLeaderChangeRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string NewLeaderId { get; }

        public NetworkClanLeaderChangeRequest(string clanId, string newLeaderId)
        {
            ClanId = clanId;
            NewLeaderId = newLeaderId;
        }
    }
}