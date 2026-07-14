using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Identifies the vanilla mutation represented by one entry in a troop-roster element batch.
/// </summary>
internal enum TroopRosterElementOperationKind
{
    AddCounts = 0,
    SetXp = 1,
}

/// <summary>
/// One ordered mutation for a single troop-roster element. Add-count operations retain every vanilla
/// argument because wounded clamping and remove/recreate behavior make general delta summation unsafe.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct TroopRosterElementOperation
{
    [ProtoMember(1)]
    public readonly TroopRosterElementOperationKind Kind;

    [ProtoMember(2)]
    public readonly int Count;

    [ProtoMember(3)]
    public readonly int WoundedCount;

    [ProtoMember(4)]
    public readonly int Xp;

    [ProtoMember(5)]
    public readonly bool RemoveDepleted;

    private TroopRosterElementOperation(TroopRosterElementOperationKind kind, int count,
        int woundedCount, int xp, bool removeDepleted)
    {
        Kind = kind;
        Count = count;
        WoundedCount = woundedCount;
        Xp = xp;
        RemoveDepleted = removeDepleted;
    }

    public static TroopRosterElementOperation AddCounts(int count, int woundedCount, int xpChange,
        bool removeDepleted) =>
        new TroopRosterElementOperation(TroopRosterElementOperationKind.AddCounts, count,
            woundedCount, xpChange, removeDepleted);

    public static TroopRosterElementOperation SetXp(int xp) =>
        new TroopRosterElementOperation(TroopRosterElementOperationKind.SetXp, 0, 0, xp, false);
}
