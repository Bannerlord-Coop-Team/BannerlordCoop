namespace Missions.Naval;

/// <summary>A fleet's deployment slot on its side's spawn path: its front offset and what kept it from fitting, if anything.</summary>
internal readonly struct NavalSideSlot
{
    public readonly float Offset;
    public readonly NavalSlotProblem Problem;

    // Holds the slot's front offset and its problem.
    public NavalSideSlot(float offset, NavalSlotProblem problem)
    {
        Offset = offset;
        Problem = problem;
    }
}

/// <summary>Why a deployment slot isn't where its fleet fits.</summary>
public enum NavalSlotProblem
{
    /// <summary>The fleet fits there.</summary>
    None,

    /// <summary>No free stretch of the path had room for the fleet, so it can overlap the fleet behind it.</summary>
    NoRoomOnPath,

    /// <summary>The fleet would have reached the opposing base, so it is held a fleet and a gap short of it.</summary>
    HeldShortOfOpposingBase,
}
