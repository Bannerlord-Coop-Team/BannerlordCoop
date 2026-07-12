using Common.Messaging;
using System;
using TaleWorlds.Library;

namespace Missions.Missiles.Message;

/// <summary>
/// Local notification that a received missile now has a native visual on this client.
/// Carries the source launch identity and data needed to keep a delayed routed hit behind the locally replayed
/// projectile for approximately its original flight interval.
/// </summary>
public readonly struct MissileReconstructed : IEvent
{
    public Guid AgentId { get; }
    public int SourceMissileIndex { get; }
    public long ShotSequence { get; }
    public Vec3 Position { get; }
    public Vec3 Direction { get; }
    public float BaseSpeed { get; }
    public float Speed { get; }
    public bool IsFastForwarded { get; }
    public float RemainingFlightSeconds { get; }

    public MissileReconstructed(Guid agentId, int sourceMissileIndex, long shotSequence, Vec3 position,
        Vec3 direction, float baseSpeed, float speed, bool isFastForwarded = false,
        float remainingFlightSeconds = 0f)
    {
        AgentId = agentId;
        SourceMissileIndex = sourceMissileIndex;
        ShotSequence = shotSequence;
        Position = position;
        Direction = direction;
        BaseSpeed = baseSpeed;
        Speed = speed;
        IsFastForwarded = isFastForwarded;
        RemainingFlightSeconds = remainingFlightSeconds;
    }
}
