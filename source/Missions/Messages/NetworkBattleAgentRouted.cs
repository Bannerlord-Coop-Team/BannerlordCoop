using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Owner → peers (over the mesh): an agent the sender had authority over routed out of the battle, so
/// every client despawns its puppet of it. A rout is not a casualty — the troop stays in its roster.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleAgentRouted : IEvent
{
    [ProtoMember(1)]
    public readonly Guid AgentId;

    public NetworkBattleAgentRouted(Guid agentId)
    {
        AgentId = agentId;
    }
}
