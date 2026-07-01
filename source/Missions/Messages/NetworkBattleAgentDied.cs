using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Owner → peers (over the mesh): an agent the sender had authority over died, so every client kills its
/// puppet of it. Sent only by the agent's owner (the host for AI, the player for their hero); receivers
/// apply it and never re-broadcast (their copy is not locally controlled).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleAgentDied : IEvent
{
    [ProtoMember(1)]
    public readonly Guid AgentId;
    [ProtoMember(2)]
    public readonly bool Wounded;

    public NetworkBattleAgentDied(Guid agentId, bool wounded)
    {
        AgentId = agentId;
        Wounded = wounded;
    }
}
