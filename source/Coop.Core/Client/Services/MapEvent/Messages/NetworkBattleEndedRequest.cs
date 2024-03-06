using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.MapEvent.Messages
{
    /// <summary>
    /// Request from client to server to end battle
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkBattleEndedRequest : ICommand
    {
        [ProtoMember(1)]
        public string partyId { get; }

        public NetworkBattleEndedRequest(string partyId)
        {
            this.partyId = partyId;
        }
    }
}