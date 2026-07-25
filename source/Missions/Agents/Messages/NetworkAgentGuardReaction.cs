using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Agents.Messages;

/// <summary>
/// Replicates the defender's native one-shot melee guard reaction from the collision-authority peer.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAgentGuardReaction : IEvent
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
    public int ReactionChannel { get; }

    [ProtoMember(7)]
    public int ReactionActionIndex { get; }

    [ProtoMember(8)]
    public float Progress { get; }

    [ProtoMember(9)]
    public ulong AnimationFlags { get; }

    [ProtoMember(10)]
    public bool IsMounted { get; }

    public NetworkAgentGuardReaction(
        string sourceControllerId,
        long sequence,
        int battleHostEpoch,
        Guid attackerAgentId,
        Guid agentId,
        int reactionChannel,
        int reactionActionIndex,
        float progress,
        ulong animationFlags,
        bool isMounted)
    {
        SourceControllerId = sourceControllerId;
        Sequence = sequence;
        BattleHostEpoch = battleHostEpoch;
        AttackerAgentId = attackerAgentId;
        AgentId = agentId;
        ReactionChannel = reactionChannel;
        ReactionActionIndex = reactionActionIndex;
        Progress = progress;
        AnimationFlags = animationFlags;
        IsMounted = isMounted;
    }
}
