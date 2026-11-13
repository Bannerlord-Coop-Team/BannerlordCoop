namespace Missions.Naval;

/// <summary>A side's player slots by rank and the opposing base they face.</summary>
internal readonly struct NavalSideLayout
{
    public readonly NavalSideSlot[] Slots;
    public readonly float OpposingBase;

    // Holds the slots by rank and the opposing base.
    public NavalSideLayout(NavalSideSlot[] slots, float opposingBase)
    {
        Slots = slots;
        OpposingBase = opposingBase;
    }
}
