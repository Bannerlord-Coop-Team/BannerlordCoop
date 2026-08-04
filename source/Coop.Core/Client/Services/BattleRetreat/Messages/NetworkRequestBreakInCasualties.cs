using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.BattleRetreat.Messages;

/// <summary>
/// Client asks the server to apply its break-in losses. Carries no casualty data - the server decides.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestBreakInCasualties : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }

    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkRequestBreakInCasualties(string partyId, string settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
