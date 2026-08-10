using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;
using System;

namespace Coop.Core.Server.Connections.Messages;

public enum JoinSyncSignal
{
    ReplayComplete,
    ReplayApplied,
    BaselineRequested,
    BaselineApplied,
    FinalBaselineApplied,
    WorldReady,
    CatchUpApplied,
}

/// <summary>Coordinates the ordered replay and baseline barriers for a joining client.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkJoinSync : IMessage
{
    public const int CompletionPacketThreshold = 500;

    [ProtoMember(1)]
    public readonly JoinSyncSignal Signal;

    [ProtoMember(2)]
    public readonly SettlementClaimantDecisionSnapshotData[] ClaimantSnapshots;

    public NetworkJoinSync(
        JoinSyncSignal signal,
        SettlementClaimantDecisionSnapshotData[] claimantSnapshots = null)
    {
        Signal = signal;
        ClaimantSnapshots = claimantSnapshots ?? Array.Empty<SettlementClaimantDecisionSnapshotData>();
    }
}
