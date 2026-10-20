using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>A hull body's planar velocity (m/s) and yaw rate (rad/s), sent with each sample so copies can extrapolate it.</summary>
public readonly struct BattleShipMotion
{
    public BattleShipMotion(Vec2 velocity, float yawRate)
    {
        Velocity = velocity;
        YawRate = yawRate;
    }

    public Vec2 Velocity { get; }

    public float YawRate { get; }

    /// <summary>Whether every component is a finite number.</summary>
    public bool IsValid => IsFinite(Velocity.x) && IsFinite(Velocity.y) && IsFinite(YawRate);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
