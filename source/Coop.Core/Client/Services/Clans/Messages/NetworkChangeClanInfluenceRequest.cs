using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.Clans.Messages
{
    /// <summary>
    /// Request from client to server to change influence
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkChangeClanInfluenceRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public float Amount { get; }

        public NetworkChangeClanInfluenceRequest(string clanId, float amount)
        {
            ClanId = clanId;
            Amount = amount;
        }
    }
}