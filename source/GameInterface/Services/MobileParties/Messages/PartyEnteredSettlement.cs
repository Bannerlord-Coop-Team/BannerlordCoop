using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Allow entry to a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkPartyEnteredSettlement : ICommand
    {
        [ProtoMember(1)]
        public string SettlementId;
        [ProtoMember(2)]
        public string PartyId;

        public NetworkPartyEnteredSettlement(string settlementId, string partyId)
        {
            SettlementId = settlementId;
            PartyId = partyId;
        }
    }
}