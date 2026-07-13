namespace GameInterface.Services.MapEvents;

/// <summary>
/// Cross-assembly state for the battle-conclusion patches. In a coop battle mission only the elected
/// battle host may relay a mission-derived victory to the server — a peer's mission can diverge from
/// the host's (e.g. commit before the host's troops ever arrived) and locally conclude a victory that
/// must not finalize the real battle. Kept current by the Missions battle controller; both fields are
/// only touched on the game thread. Battles without a mission (menu simulation, hideouts) have no
/// controller, so the gate stays inactive and their conclusions relay as before.
/// </summary>
public static class BattleConclusionGate
{
    public static bool IsInCoopBattleMission;

    public static bool IsLocalBattleHost;

    public static bool SuppressNextHostVictoryRelay;
}
