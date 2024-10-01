using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.MapEvent.Messages
{
    /// <summary>
    /// Request from client to server to start battle
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkBattleStartedRequest : ICommand
    {
        [ProtoMember(1)]
        public string attackerPartyId { get; }
        [ProtoMember(2)]
        public string defenderPartyId { get; }

        public NetworkBattleStartedRequest(string attackerPartyId, string defenderPartyId)
        {
            this.attackerPartyId = attackerPartyId;
            this.defenderPartyId = defenderPartyId;
        }
    }
}