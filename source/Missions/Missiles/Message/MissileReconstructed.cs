using Common.Messaging;
using System;
using TaleWorlds.Library;

namespace Missions.Missiles.Message;

/// <summary>
/// Local notification that a received missile now has a native visual on this client.
/// </summary>
public readonly struct MissileReconstructed : IEvent
{
    public Guid AgentId { get; }
    public long ShotSequence { get; }
    public string MissileItemId { get; }
    public Vec3 Position { get; }
    public float Speed { get; }
    public float RemainingFlightSeconds { get; }

    public MissileReconstructed(Guid agentId, long shotSequence, string missileItemId,
        Vec3 position, float speed, float remainingFlightSeconds)
    {
        AgentId = agentId;
        ShotSequence = shotSequence;
        MissileItemId = missileItemId;
        Position = position;
        Speed = speed;
        RemainingFlightSeconds = remainingFlightSeconds;
    }
}
