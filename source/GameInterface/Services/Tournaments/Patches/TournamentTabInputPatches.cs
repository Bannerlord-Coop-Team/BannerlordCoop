using GameInterface.Services.Tournaments.UI;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Tournaments.Patches;

[HarmonyPatch]
internal static class TournamentTabInputPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(Input), nameof(Input.IsKeyPressed)),
        AccessTools.Method(typeof(Input), nameof(Input.IsKeyDown)),
        AccessTools.Method(typeof(Input), nameof(Input.IsKeyDownImmediate)),
        AccessTools.Method(typeof(Input), nameof(Input.IsKeyReleased))
    };

    [HarmonyPostfix]
    private static void SuppressTab(InputKey __0, ref bool __result)
    {
        if (ShouldSuppress(__0 == InputKey.Tab, IsCoopTournamentMissionActive()))
            __result = false;
    }

    internal static bool ShouldSuppress(bool isTab, bool isCoopTournamentMissionActive)
        => isTab && isCoopTournamentMissionActive;

    private static bool IsCoopTournamentMissionActive()
    {
        return Mission.Current != null &&
            ContainerProvider.TryResolve<TournamentMissionUIContext>(out var context) &&
            context.TryGet(out _);
    }
}