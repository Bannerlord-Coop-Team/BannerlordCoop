using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Owner → peers (over the mesh): an authoritative agent began fleeing, so every client marks its puppet
/// as running away before the later routed-removal message despawns it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleAgentFleeing : IEvent
{
    [ProtoMember(1)]
    public readonly Guid AgentId;

    // The sender's authority revision of the agent, so a peer can tell a previous owner's late message from a current one.
    [ProtoMember(2)]
    public readonly long AuthorityRevision;

    public NetworkBattleAgentFleeing(Guid agentId, long authorityRevision)
    {
        AgentId = agentId;
        AuthorityRevision = authorityRevision;
    }
}
