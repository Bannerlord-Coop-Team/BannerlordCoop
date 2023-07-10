using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Message from the client requesting a settlement encounter to start
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkRequestStartSettlementEncounter : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkRequestStartSettlementEncounter(
        string partyId,
        string settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
