using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Attacker's node → peers (over the mesh): apply this much damage to an agent. Only the agent's OWNER (the
/// node with authority) acts on it — applying the damage to the real agent and, if it dies, broadcasting the
/// death via the normal path. Sent when a local troop hits a puppet, so that puppet's life and death stay
/// authoritative on its owner and the clients' battles don't diverge.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkApplyBattleDamage : IEvent
{
    [ProtoMember(1)]
    public readonly Guid VictimAgentId;
    [ProtoMember(2)]
    public readonly float Damage;

    public NetworkApplyBattleDamage(Guid victimAgentId, float damage)
    {
        VictimAgentId = victimAgentId;
        Damage = damage;
    }
}
