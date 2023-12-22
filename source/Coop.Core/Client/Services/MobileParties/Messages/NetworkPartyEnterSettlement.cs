using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Message from the server commanding a party to enter a settlement.
/// For all parties except the player party
/// </summary>
[ProtoContract(SkipConstructor = true)]
[DontLogMessage]
public record NetworkPartyEnterSettlement : ICommand
{
    [ProtoMember(1)]
    public string SettlementId;
    [ProtoMember(2)]
    public string PartyId;

    public NetworkPartyEnterSettlement(string settlementId, string partyId)
    {
        SettlementId = settlementId;
        PartyId = partyId;
    }
}
