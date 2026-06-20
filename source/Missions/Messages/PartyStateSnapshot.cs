using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Missions.Messages;

/// <summary>
/// Sent to a rejoining client just before its party's authority is handed back, so it can reconstruct the
/// party's agents to match the host's live state instead of a flag-flip. The host captures the per-agent
/// state; richer engine fields (position, mount, current action) are added by the host-side capture.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class PartyStateSnapshot : IEvent
{
    [ProtoMember(1)]
    public readonly Guid PartyId;

    [ProtoMember(2)]
    public readonly AgentSnapshot[] Agents;

    public PartyStateSnapshot(Guid partyId, AgentSnapshot[] agents)
    {
        PartyId = partyId;
        Agents = agents ?? Array.Empty<AgentSnapshot>();
    }
}

/// <summary>Per-agent state in a <see cref="PartyStateSnapshot"/>.</summary>
[ProtoContract]
public readonly struct AgentSnapshot
{
    [ProtoMember(1)]
    public readonly Guid AgentId;

    [ProtoMember(2)]
    public readonly float Health;

    [ProtoMember(3)]
    public readonly bool IsAlive;

    public AgentSnapshot(Guid agentId, float health, bool isAlive)
    {
        AgentId = agentId;
        Health = health;
        IsAlive = isAlive;
    }
}
