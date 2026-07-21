using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Reserves a non-player same-side team before allied players can join the mission.</summary>
[HarmonyPatch(typeof(MissionCombatantsLogic), nameof(MissionCombatantsLogic.OnBehaviorInitialize))]
internal static class CoopPlayerAllyTeamPatch
{
    [HarmonyPostfix]
    private static void Postfix(MissionCombatantsLogic __instance)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;

        var mission = __instance.Mission;
        var playerTeam = mission?.PlayerTeam;
        if (playerTeam == null) return;

        var allyTeam = playerTeam.Side == BattleSideEnum.Attacker
            ? mission.AttackerAllyTeam
            : mission.DefenderAllyTeam;
        if (allyTeam != null) return;

        mission.Teams.Add(playerTeam.Side, playerTeam.Color, playerTeam.Color2, playerTeam.Banner);
    }
}
