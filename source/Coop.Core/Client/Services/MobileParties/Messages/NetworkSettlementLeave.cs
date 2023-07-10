using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Message from the server commanding a party to enter a settlement.
/// For all parties except the player party
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSettlementLeave : ICommand
{
    [ProtoMember(1)]
    public string PartyId;

    public NetworkSettlementLeave(string partyId)
    {
        PartyId = partyId;
    }
}
