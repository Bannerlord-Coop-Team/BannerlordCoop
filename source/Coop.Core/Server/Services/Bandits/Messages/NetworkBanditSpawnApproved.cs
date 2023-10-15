using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Server.Services.Bandits.Messages
{
    /// <summary>
    /// Request from client to server to spawn bandit
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkBanditSpawnApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }

        public NetworkBanditSpawnApproved(string clanId)
        {
            ClanId = clanId;
        }
    }
}