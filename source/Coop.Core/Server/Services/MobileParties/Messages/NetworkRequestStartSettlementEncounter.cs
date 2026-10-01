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
    public readonly uint PartyId;

    [ProtoMember(2)]
    public readonly uint SettlementId;

    public NetworkRequestStartSettlementEncounter(
        uint partyId,
        uint settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
