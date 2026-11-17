using TaleWorlds.Library;

namespace Missions.Battles;

/// <summary>The pilot seats vanilla seats spawn crew at directly, each with its own spawn placement.</summary>
public enum StationSeatKind
{
    None,
    Oar,
    Helm,
}

/// <summary>
/// How a pilot sits at an oar or the helm right now, read from the engine: whether its seat action finished, and what its spawn
/// placement is computed from.
/// </summary>
public readonly struct StationSeatPose
{
    public StationSeatPose(StationSeatKind kind, bool isSitting, bool isStruck, MatrixFrame stationFrame, Vec3 sitDownDisplacement,
        Vec3 position)
    {
        Kind = kind;
        IsSitting = isSitting;
        IsStruck = isStruck;
        StationFrame = stationFrame;
        SitDownDisplacement = sitDownDisplacement;
        Position = position;
    }

    /// <summary>Which pilot seat the agent holds; None for any other station or user.</summary>
    public StationSeatKind Kind { get; }

    /// <summary>An oar's rower finished sitting down, or a helm's pilot reached and locked to its frame outside a capture.</summary>
    public bool IsSitting { get; }

    /// <summary>The agent plays a strike action, which a ram's stagger is.</summary>
    public bool IsStruck { get; }

    /// <summary>The station's raw global frame, not the ground-resolved user frame.</summary>
    public MatrixFrame StationFrame { get; }

    /// <summary>An oar's sit-down root displacement at its end, in the station's frame; zero at the helm.</summary>
    public Vec3 SitDownDisplacement { get; }

    /// <summary>The agent's world position.</summary>
    public Vec3 Position { get; }
}
