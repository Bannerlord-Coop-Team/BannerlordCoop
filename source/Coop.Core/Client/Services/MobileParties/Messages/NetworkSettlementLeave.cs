using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Command a party to leave a settlement.
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
