using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

public enum LocationDespawnReason : byte
{
    /// <summary>The NPC left the scene (passage exit / native removal) — peers fade it out.</summary>
    Removed = 0,

    /// <summary>The NPC died on the host. v1 peers still fade the body (SR-041 notes the blow-replay
    /// follow-up); the reason is carried so that fidelity can be added without a wire change.</summary>
    Died = 1,
}

/// <summary>
/// Location NPC host to peers over the mission mesh: agents the host's native systems removed
/// (passage exits, trickle churn) or that died host-side (SR-026/SR-041). Parallel arrays; sent on
/// the same reliable-ordered stream as the spawn batches so a spawn/despawn pair for one agent
/// applies in order.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkDespawnLocationAgents : IEvent
{
    [ProtoMember(1)]
    public readonly Guid[] AgentIds = Array.Empty<Guid>();
    [ProtoMember(2)]
    public readonly byte[] Reasons = Array.Empty<byte>();

    public NetworkDespawnLocationAgents(Guid[] agentIds, byte[] reasons)
    {
        AgentIds = agentIds ?? Array.Empty<Guid>();
        Reasons = reasons ?? Array.Empty<byte>();
    }
}
