using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Messages;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Handlers
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkStartSettlementEncounter : ICommand
    {
        [ProtoMember(1)]
        public string SettlementId;
        [ProtoMember(2)]
        public string PartyId;

        public NetworkStartSettlementEncounter(NetworkRequestSettlementEncounter payload)
        {
            SettlementId = payload.SettlementId;
            PartyId = payload.PartyId;
        }
    }
}