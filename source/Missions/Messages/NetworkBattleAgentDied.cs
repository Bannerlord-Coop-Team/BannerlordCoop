using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>
/// Owner → peers (over the mesh): an agent the sender had authority over died, so every client kills its
/// puppet of it. Sent only by the agent's owner; receivers apply it and never re-broadcast because their
/// copy is not locally controlled.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleAgentDied : IEvent
{
    [ProtoMember(1)]
    public readonly Guid AgentId;
    [ProtoMember(2)]
    public readonly bool Wounded;
    [ProtoMember(3)]
    public readonly Guid AffectorAgentId;
    [ProtoMember(4)]
    public readonly KillingBlow KillingBlow;

    public NetworkBattleAgentDied(Guid agentId, bool wounded, Guid affectorAgentId, KillingBlow killingBlow)
    {
        AgentId = agentId;
        Wounded = wounded;
        AffectorAgentId = affectorAgentId;
        KillingBlow = killingBlow;
    }
}
