using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>
/// The crew weight an owner's hull physics last committed and its centre in the hull body's local frame, so a copy can
/// heel under the owner's load. Immutable, so the physics thread reads it through one reference.
/// </summary>
public sealed class BattleShipCrewLoad
{
    private BattleShipCrewLoad(float mass, Vec3 localCentre)
    {
        Mass = mass;
        LocalCentre = localCentre;
    }

    /// <summary>The summed mass of the agents on deck in kg.</summary>
    public float Mass { get; }

    /// <summary>Their mass-weighted centre in the hull body's local frame.</summary>
    public Vec3 LocalCentre { get; }

    /// <summary>The load, or null (unknown) when the mass is not positive or any value is not a finite number.</summary>
    public static BattleShipCrewLoad Create(float mass, Vec3 localCentre)
    {
        if (!(mass > 0f) || !IsFinite(mass)) return null;
        if (!IsFinite(localCentre.x) || !IsFinite(localCentre.y) || !IsFinite(localCentre.z)) return null;

        return new BattleShipCrewLoad(mass, localCentre);
    }

    // Whether the value is neither NaN nor infinite.
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
