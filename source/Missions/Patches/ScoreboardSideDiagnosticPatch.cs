using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.ViewModelCollection.Scoreboard;

namespace Missions.Patches;

// Temporary diagnostic for #1704/#1705: logs every TroopNumberChanged call the scoreboard VM
// receives, so a divergent kill/wound/rout split between clients can be traced to the exact
// side/character/counts each client's local scoreboard was told about. Remove once resolved.
[HarmonyPatch(typeof(CustomBattleScoreboardVM))]
internal class ScoreboardSideDiagnosticPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ScoreboardSideDiagnosticPatch>();

    [HarmonyPatch(nameof(CustomBattleScoreboardVM.TroopNumberChanged))]
    [HarmonyPrefix]
    private static void Prefix(BattleSideEnum side, IBattleCombatant battleCombatant, BasicCharacterObject character,
        int number, int numberDead, int numberWounded, int numberRouted, int numberKilled, int numberReadyToUpgrade)
    {
        Logger.Information(
            "[SideDiag] Scoreboard.TroopNumberChanged: side={Side} combatant={Combatant} char={Char} number={Number} dead={Dead} wounded={Wounded} routed={Routed} killed={Killed}",
            side, battleCombatant, character, number, numberDead, numberWounded, numberRouted, numberKilled);
    }
}
