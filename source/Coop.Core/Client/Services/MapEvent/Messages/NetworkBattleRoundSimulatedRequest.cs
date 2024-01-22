using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.MapEvent.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkBattleRoundSimulatedRequest : ICommand
    {
        [ProtoMember(1)]
        public string PartyId { get; }
        [ProtoMember(2)]    
        public int Side { get; }
        [ProtoMember(3)]
        public float Advantage { get; }

        public NetworkBattleRoundSimulatedRequest(string partyId, int side, float advantage)
        {
            PartyId = partyId;
            Side = side;
            Advantage = advantage;
        }
    }
}