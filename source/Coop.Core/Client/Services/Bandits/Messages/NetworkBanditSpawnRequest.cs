using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.Bandits.Messages
{
    /// <summary>
    /// Request from client to server to spawn bandit
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkBanditSpawnRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }

        public NetworkBanditSpawnRequest(string clanId)
        {
            ClanId = clanId;
        }
    }
}