namespace GameInterface.Services.Locations
{
    /// <summary>
    /// Marker for the P2P mission behaviors (in the Missions assembly) that must be attached to a
    /// location/interior mission when it opens. GameInterface cannot reference the Missions types
    /// directly (Missions depends on GameInterface, not the reverse), so the
    /// <see cref="Patches.PlayerLocationEntryPatches"/> postfix resolves all implementations from the
    /// shared container (<c>Missions.ContainerProvider</c>) as <c>IEnumerable&lt;ILocationMissionBehavior&gt;</c>
    /// and adds them to the freshly opened mission. Implementors are TaleWorlds <c>MissionBehavior</c>s.
    /// </summary>
    public interface ILocationMissionBehavior
    {
    }
}
