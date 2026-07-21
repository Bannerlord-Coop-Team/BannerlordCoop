namespace GameInterface.Services.MapEvents;

/// <summary>
/// Cross-assembly state for the battle-conclusion patches. Coop mission victories are reported through
/// NetworkBattleResultReady so the server can wait for every member and accepted joiner. Battles without a
/// mission have no controller, so their legacy battle-state conclusions relay as before.
/// </summary>
public static class BattleConclusionGate
{
    public static bool IsInCoopBattleMission;
}
