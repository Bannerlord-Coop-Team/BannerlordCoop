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

    public NetworkBattleAgentFleeing(Guid agentId)
    {
        AgentId = agentId;
    }
}
