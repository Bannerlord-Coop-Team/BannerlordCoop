using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>
/// The crew weight an owner's hull physics last committed and its centre in the hull body's local frame, so a copy can
/// heel under the owner's load. Immutable, so the fixed tick reads it through one reference.
/// </summary>
public sealed class BattleShipCrewLoad
{
    /// <summary>A known-empty deck: no agent stands on the owner's hull, so the copy takes no crew weight.</summary>
    public static readonly BattleShipCrewLoad Empty = new BattleShipCrewLoad(0f, Vec3.Zero);

    // A load already checked by Create.
    private BattleShipCrewLoad(float mass, Vec3 localCentre)
    {
        Mass = mass;
        LocalCentre = localCentre;
    }

    /// <summary>The summed mass of the agents on deck in kg.</summary>
    public float Mass { get; }

    /// <summary>Their mass-weighted centre in the hull body's local frame.</summary>
    public Vec3 LocalCentre { get; }

    /// <summary>Whether the owner's deck is known to carry nobody.</summary>
    public bool IsEmpty => Mass == 0f;

    /// <summary>The load; <see cref="Empty"/> for a zero mass; null when the mass is negative or any value is not finite.</summary>
    public static BattleShipCrewLoad Create(float mass, Vec3 localCentre)
    {
        if (!IsFinite(mass) || mass < 0f) return null;
        if (mass == 0f) return Empty;
        if (!IsFinite(localCentre.x) || !IsFinite(localCentre.y) || !IsFinite(localCentre.z)) return null;

        return new BattleShipCrewLoad(mass, localCentre);
    }

    // Whether the value is neither NaN nor infinite.
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
