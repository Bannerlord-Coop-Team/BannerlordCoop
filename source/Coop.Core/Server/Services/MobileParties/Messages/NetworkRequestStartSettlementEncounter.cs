using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Message from the client requesting a settlement encounter to start.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestStartSettlementEncounter : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;

    [ProtoMember(2)]
    public readonly string SettlementId;

    public NetworkRequestStartSettlementEncounter(
        string partyId,
        string settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}