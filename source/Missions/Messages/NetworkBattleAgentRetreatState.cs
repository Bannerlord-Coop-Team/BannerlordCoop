using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Owner → peers (over the mesh): an authoritative agent's retreat flag changed. Unlike
/// <see cref="NetworkBattleAgentFleeing"/> it carries no panic, so peers only mirror the flag vanilla's end checks read.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleAgentRetreatState : IEvent
{
    [ProtoMember(1)]
    public readonly Guid AgentId;

    [ProtoMember(2)]
    public readonly bool IsRunningAway;

    // The sender's authority revision of the agent, so a peer can tell a previous owner's late message from a current one.
    [ProtoMember(3)]
    public readonly long AuthorityRevision;

    public NetworkBattleAgentRetreatState(Guid agentId, bool isRunningAway, long authorityRevision)
    {
        AgentId = agentId;
        IsRunningAway = isRunningAway;
        AuthorityRevision = authorityRevision;
    }
}
