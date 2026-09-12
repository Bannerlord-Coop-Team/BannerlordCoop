using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// A siege started preparing; a client whose party is inside the settlement switches to the vanilla
/// siege-preparation menu.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPromptSiegePreparation : IEvent
{
    [ProtoMember(1)]
    public string AttackerPartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkPromptSiegePreparation(string attackerPartyId, string settlementId)
    {
        AttackerPartyId = attackerPartyId;
        SettlementId = settlementId;
    }
}
