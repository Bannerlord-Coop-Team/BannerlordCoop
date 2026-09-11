using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>Current holder to campaign server: an exact retained-hero surrender for membership validation.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkRequestRetainedPlayerHero : IEvent
{
    [ProtoMember(1)] public readonly NetworkRetainedPlayerHero Handoff;

    public NetworkRequestRetainedPlayerHero(NetworkRetainedPlayerHero handoff)
    {
        Handoff = handoff;
    }
}

/// <summary>The current battle host relinquishes one retained hero and its mount to the returning player.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkRetainedPlayerHero : IEvent
{
    [ProtoMember(1)] public readonly string BattleInstanceId;
    [ProtoMember(2)] public readonly int HostEpoch;
    [ProtoMember(3)] public readonly string ReturningControllerId;
    [ProtoMember(4)] public readonly BattleAgentSpawnData Previous;

    public NetworkRetainedPlayerHero(string battleInstanceId, int hostEpoch,
        string returningControllerId, BattleAgentSpawnData previous)
    {
        BattleInstanceId = battleInstanceId;
        HostEpoch = hostEpoch;
        ReturningControllerId = returningControllerId;
        Previous = previous;
    }

    public bool HasValidAuthorityTransition => Previous != null
        && Previous.AgentId != Guid.Empty
        && !string.IsNullOrEmpty(BattleInstanceId)
        && HostEpoch > 0
        && !string.IsNullOrEmpty(ReturningControllerId)
        && !string.IsNullOrEmpty(Previous.OwnerControllerId)
        && ReturningControllerId != Previous.OwnerControllerId
        && Previous.AuthorityRevision > 0 && Previous.AuthorityRevision < long.MaxValue
        && (Previous.MountAgentId == Guid.Empty
            || (Previous.MountAgentId != Previous.AgentId
                && Previous.MountAuthorityRevision >= 0 && Previous.MountAuthorityRevision < long.MaxValue));

    public BattleAgentSpawnData CreateReturnedRecord()
    {
        if (!HasValidAuthorityTransition) throw new InvalidOperationException("Invalid retained hero authority transition.");
        var data = Previous;
        return new BattleAgentSpawnData(
            data.AgentId, data.CharacterId, data.Position, data.Side, data.Health,
            ReturningControllerId, data.MapEventPartyId, data.TroopSeed,
            data.SpawnEquipment, data.BodyProperties, data.MissionEquipmentData,
            data.MountAgentId, data.FormationIndex, data.MovementId, data.MountMovementId,
            data.OriginalOwnerControllerId, data.HasCurrentEquipment ? data.CurrentEquipment : null,
            data.MovementScopeId, data.MountOriginalOwnerControllerId, data.MountMovementScopeId,
            data.IsRunningAway, data.AuthorityRevision + 1,
            data.MountAgentId == Guid.Empty ? data.MountAuthorityRevision : data.MountAuthorityRevision + 1);
    }
}
