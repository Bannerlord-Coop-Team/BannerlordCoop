using Common.Messaging;
using ProtoBuf;
using System;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// A siege dissolved without a completed battle. Captured participant ids let each affected client
/// leave its stale siege UI using the role it held before the server destroyed the siege graph.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPromptSiegeEnded : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public bool BesiegerDefeated { get; }
    [ProtoMember(3)]
    public string LeaderPartyId { get; }
    [ProtoMember(4)]
    public string[] AttackerPartyIds { get; }
    [ProtoMember(5)]
    public string[] DefenderPartyIds { get; }
    [ProtoMember(6)]
    public bool InterruptedActiveAssault { get; }

    public NetworkPromptSiegeEnded(
        string settlementId,
        bool besiegerDefeated,
        string leaderPartyId,
        string[] attackerPartyIds,
        string[] defenderPartyIds,
        bool interruptedActiveAssault = false)
    {
        SettlementId = settlementId;
        BesiegerDefeated = besiegerDefeated;
        LeaderPartyId = leaderPartyId;
        AttackerPartyIds = attackerPartyIds ?? Array.Empty<string>();
        DefenderPartyIds = defenderPartyIds ?? Array.Empty<string>();
        InterruptedActiveAssault = interruptedActiveAssault;
    }
}
