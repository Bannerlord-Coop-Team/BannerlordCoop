namespace Missions.Naval;

/// <summary>A fleet's deployment slot on its side's spawn path: its front offset, the offset it faces and whether it fit.</summary>
public readonly struct NavalSideSlot
{
    public readonly float Offset;
    public readonly float TargetOffset;
    public readonly bool Fits;

    // Holds the slot's front offset, the opposing base it faces and whether the path had room for it.
    public NavalSideSlot(float offset, float targetOffset, bool fits)
    {
        Offset = offset;
        TargetOffset = targetOffset;
        Fits = fits;
    }
}
