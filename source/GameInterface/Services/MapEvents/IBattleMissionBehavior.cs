namespace GameInterface.Services.MapEvents
{
    /// <summary>
    /// Marker for the P2P mission behaviors (in the Missions assembly) that must be attached to a
    /// field-battle mission when it opens — the battle counterpart to
    /// <see cref="Locations.ILocationMissionBehavior"/>. GameInterface cannot reference the Missions
    /// types directly (Missions depends on GameInterface, not the reverse), so
    /// <see cref="Patches.BattleMissionEntryPatch"/> resolves all implementations from the shared
    /// container (<see cref="ContainerProvider"/>) as <c>IEnumerable&lt;IBattleMissionBehavior&gt;</c>
    /// and adds them to the freshly opened mission. Implementors are TaleWorlds <c>MissionBehavior</c>s.
    /// </summary>
    public interface IBattleMissionBehavior
    {
    }
}
