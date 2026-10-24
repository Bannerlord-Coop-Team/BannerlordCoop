using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>
/// A hull body's planar velocity (m/s), yaw rate (rad/s) and crew load, sent with each sample so copies can extrapolate it
/// and heel under the owner's crew.
/// </summary>
public readonly struct BattleShipMotion
{
    /// <summary>The motion of one sample; no crew load leaves it unknown.</summary>
    public BattleShipMotion(Vec2 velocity, float yawRate, BattleShipCrewLoad crewLoad = null)
    {
        Velocity = velocity;
        YawRate = yawRate;
        CrewLoad = crewLoad;
    }

    public Vec2 Velocity { get; }

    public float YawRate { get; }

    /// <summary>
    /// The owner's committed crew load, <see cref="BattleShipCrewLoad.Empty"/> for a deck known to be empty; null when the
    /// sample carries none (a frame that committed no weight while agents stood aboard).
    /// </summary>
    public BattleShipCrewLoad CrewLoad { get; }

    /// <summary>Whether the velocity and yaw rate are finite numbers; a bad crew load arrives as unknown instead.</summary>
    public bool IsValid => IsFinite(Velocity.x) && IsFinite(Velocity.y) && IsFinite(YawRate);

    // Whether the value is neither NaN nor infinite.
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
