#if DEBUG
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

[HarmonyPatch(
    typeof(Mission),
    nameof(Mission.TickAgentsAndTeamsImp),
    new[] { typeof(float), typeof(bool) })]
[HarmonyPatchCategory(MissionModule.LiveTestInputPatchCategory)]
internal static class JoustInputBoundaryPatch
{
    [HarmonyPrefix]
    private static void Prefix(Mission __instance)
    {
        BattleDebugCommands.ApplyJoustInputAtNativeTickBoundary(__instance);
    }
}
#endif
