using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    /// <summary>
    /// Clan destruction is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkDestroyClanApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public int DetailId { get; }

        public NetworkDestroyClanApproved(string clanId, int detailId)
        {
            ClanId = clanId;
            DetailId = detailId;
        }
    }
}