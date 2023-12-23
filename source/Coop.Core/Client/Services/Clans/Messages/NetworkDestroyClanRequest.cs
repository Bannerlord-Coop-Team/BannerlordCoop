using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Clans.Messages
{
    /// <summary>
    /// Request from client to server to destroy clan
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkDestroyClanRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public int DetailId { get; }

        public NetworkDestroyClanRequest(string clanId, int detailId)
        {
            ClanId = clanId;
            DetailId = detailId;
        }
    }
}