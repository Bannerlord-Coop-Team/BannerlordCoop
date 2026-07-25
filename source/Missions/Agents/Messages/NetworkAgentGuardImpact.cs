using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Agents.Messages;

/// <summary>
/// Replicates the defender's visual-only melee block impact from the collision-authority peer.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAgentGuardImpact : IEvent
{
    [ProtoMember(1)]
    public string SourceControllerId { get; }

    [ProtoMember(2)]
    public long Sequence { get; }

    [ProtoMember(3)]
    public int BattleHostEpoch { get; }

    [ProtoMember(4)]
    public Guid AttackerAgentId { get; }

    [ProtoMember(5)]
    public Guid AgentId { get; }

    [ProtoMember(6)]
    public int Channel { get; }

    [ProtoMember(7)]
    public int GuardActionIndex { get; }

    [ProtoMember(8)]
    public int AnimationIndex { get; }

    [ProtoMember(9)]
    public float Progress { get; }

    [ProtoMember(10)]
    public float Speed { get; }

    [ProtoMember(11)]
    public float Duration { get; }

    public NetworkAgentGuardImpact(
        string sourceControllerId,
        long sequence,
        int battleHostEpoch,
        Guid attackerAgentId,
        Guid agentId,
        int channel,
        int guardActionIndex,
        int animationIndex,
        float progress,
        float speed,
        float duration)
    {
        SourceControllerId = sourceControllerId;
        Sequence = sequence;
        BattleHostEpoch = battleHostEpoch;
        AttackerAgentId = attackerAgentId;
        AgentId = agentId;
        Channel = channel;
        GuardActionIndex = guardActionIndex;
        AnimationIndex = animationIndex;
        Progress = progress;
        Speed = speed;
        Duration = duration;
    }
}
