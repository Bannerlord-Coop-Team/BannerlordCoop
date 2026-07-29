using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages;

/// <summary>A locally authoritative collision that Bannerlord resolved as guarded.</summary>
public sealed class LocalAgentGuardedHit : IEvent
{
    public Agent AffectedAgent { get; }
    public Agent AffectorAgent { get; }
    public Blow Blow { get; }
    public AttackCollisionData CollisionData { get; }

    public LocalAgentGuardedHit(
        Agent affectedAgent,
        Agent affectorAgent,
        in Blow blow,
        in AttackCollisionData collisionData)
    {
        AffectedAgent = affectedAgent;
        AffectorAgent = affectorAgent;
        Blow = blow;
        CollisionData = collisionData;
    }
}
