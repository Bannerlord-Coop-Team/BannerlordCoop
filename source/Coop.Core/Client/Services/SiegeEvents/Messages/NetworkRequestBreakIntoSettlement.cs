using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEvents.Messages;

/// <summary>
/// Client reports that its party broke into a besieged settlement to reinforce the defenders.
/// </summary>
/// <remarks>
/// This is a report rather than a request: vanilla's break-in consequence dereferences the entered
/// settlement immediately, so the client has to apply the move locally to avoid throwing. The server
/// still validates it and is free to refuse, in which case it puts the party back outside.
/// </remarks>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestBreakIntoSettlement : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkRequestBreakIntoSettlement(string partyId, string settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
