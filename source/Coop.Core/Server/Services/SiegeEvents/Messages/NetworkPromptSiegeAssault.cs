using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// A siege assault started; a besieging client adopts the replicated assault map event as its
/// player encounter so it can enter the wall-assault mission.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPromptSiegeAssault : IEvent
{
    [ProtoMember(1)]
    public string AttackerPartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkPromptSiegeAssault(string attackerPartyId, string settlementId)
    {
        AttackerPartyId = attackerPartyId;
        SettlementId = settlementId;
    }
}
