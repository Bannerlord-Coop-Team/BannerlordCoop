using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

internal enum SettlementEncounterLeaveOutcome
{
    Applied,
    Suppressed,
}

/// <summary>
/// Reports whether the server applied or suppressed a settlement encounter leave.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkSettlementEncounterLeaveResult : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;

    [ProtoMember(2)]
    public readonly SettlementEncounterLeaveOutcome Outcome;

    public NetworkSettlementEncounterLeaveResult(
        string partyId,
        SettlementEncounterLeaveOutcome outcome)
    {
        PartyId = partyId;
        Outcome = outcome;
    }
}
