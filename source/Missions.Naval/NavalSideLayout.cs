namespace Missions.Naval;

/// <summary>A side's player slots by rank, the opposing base they face and the offset no fleet of the side goes past.</summary>
internal readonly struct NavalSideLayout
{
    public readonly NavalSideSlot[] Slots;
    public readonly float OpposingBase;
    public readonly float ForwardLimit;

    // Holds the slots by rank, the opposing base and the forward limit.
    public NavalSideLayout(NavalSideSlot[] slots, float opposingBase, float forwardLimit)
    {
        Slots = slots;
        OpposingBase = opposingBase;
        ForwardLimit = forwardLimit;
    }
}
