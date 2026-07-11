using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// A siege assault started; a client whose party is inside the settlement opens its defender
/// encounter prompt.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPromptSiegeDefense : IEvent
{
    [ProtoMember(1)]
    public string AttackerPartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkPromptSiegeDefense(string attackerPartyId, string settlementId)
    {
        AttackerPartyId = attackerPartyId;
        SettlementId = settlementId;
    }
}
