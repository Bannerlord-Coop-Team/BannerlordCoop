using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Message from the server commanding a party to enter a settlement.
/// For all parties except the player party
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPartyEnterSettlement : ICommand
{
    [ProtoMember(1)]
    public uint SettlementId;
    [ProtoMember(2)]
    public uint PartyId;

    public NetworkPartyEnterSettlement(uint settlementId, uint partyId)
    {
        SettlementId = settlementId;
        PartyId = partyId;
    }
}
