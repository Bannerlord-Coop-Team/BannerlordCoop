namespace GameInterface.Services.MapEvents;

/// <summary>
/// Optional per-open inputs threaded to a <see cref="IBattleMissionInitializer"/> that a mission scene
/// must be built from a server-snapshotted value rather than live campaign state. Field and raid opens
/// pass no context (their initializers ignore it); the siege open carries the snapshot
/// <see cref="WallLevel"/> so a late joiner loads the same wall scene as the first entrant even while the
/// campaign-side siege container keeps syncing on their machine.
/// </summary>
internal sealed class BattleMissionStartContext
{
    /// <summary>
    /// The besieged settlement's wall level as snapshotted on the server when the assault opened. Null when
    /// no siege snapshot is carried; the siege initializer fails loudly rather than reading live wall state.
    /// </summary>
    public int? WallLevel { get; }

    public BattleMissionStartContext(int? wallLevel = null)
    {
        WallLevel = wallLevel;
    }
}
