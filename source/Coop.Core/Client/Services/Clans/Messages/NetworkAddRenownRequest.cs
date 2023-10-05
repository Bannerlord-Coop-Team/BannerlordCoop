using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.Clans.Messages
{
    /// <summary>
    /// Request from client to server to add renown
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkAddRenownRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public float Amount { get; }
        [ProtoMember(3)]
        public bool ShouldNotify { get; }

        public NetworkAddRenownRequest(string clanId, float amount, bool shouldNotify)
        {
            ClanId = clanId;
            Amount = amount;
            ShouldNotify = shouldNotify;
        }
    }
}