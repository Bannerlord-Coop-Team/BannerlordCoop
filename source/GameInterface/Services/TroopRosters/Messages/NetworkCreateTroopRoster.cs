using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateTroopRoster : ICommand
    {
        public string TroopRosterId { get; }
        public string PartyBaseId { get; }

        public NetworkCreateTroopRoster(string troopRosterId, string partyBaseId = null)
        {
            TroopRosterId = troopRosterId;
            PartyBaseId = partyBaseId;
        }
    }
}
