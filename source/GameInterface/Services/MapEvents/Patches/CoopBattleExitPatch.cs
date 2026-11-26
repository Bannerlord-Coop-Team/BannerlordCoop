using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Lets winners leave an ended field battle despite nearby enemies and keeps a siege defender's retreat personal.
/// </summary>
[HarmonyPatch(typeof(BattleEndLogic), nameof(BattleEndLogic.TryExit))]
internal class CoopBattleExitPatch
{
    [HarmonyPostfix]
    private static void Postfix(BattleEndLogic __instance, ref BattleEndLogic.ExitResult __result)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!BattleSpawnGate.IsCoopBattleActive) return;

        if (__result == BattleEndLogic.ExitResult.SurrenderSiege)
        {
            __result = BattleEndLogic.ExitResult.NeedsPlayerConfirmation;
            return;
        }

        var mission = __instance.Mission;
        if (__result != BattleEndLogic.ExitResult.False || GameNetwork.IsClientOrReplay ||
            !mission.IsFieldBattle || !mission.MissionEnded ||
            mission.MissionResult is not { PlayerVictory: true, EnemyRetreated: true }) return;

        // A puppet can still count as a nearby enemy after its owner leaves and the battle is won.
        mission.EndMission();
        __result = BattleEndLogic.ExitResult.True;
    }
}
