using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// TEMP diagnostic: a finalizer on <c>MissionBattleSideSpawnContext.SpawnTroops</c> that logs whether the
/// per-side spawn threw (and the full exception) or how many it spawned. The coop player side supplies its
/// origins but produces zero agents, and the tick-robustness patch swallows any throw — this surfaces it
/// (e.g. a formation-positioning / deployment-plan issue on the player team). Does NOT swallow the exception
/// (void finalizer that leaves <c>__exception</c> untouched), so behaviour is unchanged. Remove once solid.
/// </summary>
[HarmonyPatch(typeof(MissionBattleSideSpawnContext), nameof(MissionBattleSideSpawnContext.SpawnTroops),
    new[] { typeof(int), typeof(bool) })]
internal class BattleSpawnDiagnosticPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleSpawnDiagnosticPatch>();

    [HarmonyFinalizer]
    private static void Finalizer(Exception __exception, MissionBattleSideSpawnContext __instance, int number, bool isReinforcement, int __result)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;

        if (__exception != null)
            Logger.Error(__exception, "[BattleDiag] SpawnTroops(isPlayerSide={Player}, number={Num}, reinforcement={Reinf}) THREW",
                __instance.IsPlayerSide, number, isReinforcement);
        else
            Logger.Information("[BattleDiag] SpawnTroops(isPlayerSide={Player}, number={Num}, reinforcement={Reinf}) -> {Result} spawned",
                __instance.IsPlayerSide, number, isReinforcement, __result);
    }
}
