using GameInterface.Policies;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;

namespace Missions.Naval;

// Ship.OnShipDamaged destroys a ship at 0 HP; for a snapshot that would null the owner the mission still reads and raise
// OnShipDestroyed for a ship no party holds. ApplyInternal, not the tiny Apply the JIT may inline into OnShipDamaged.
[HarmonyPatch(typeof(DestroyShipAction), nameof(DestroyShipAction.ApplyInternal))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class SnapshotShipDestroyPatch
{
    // Skips the destroy for a snapshot; campaign ships run it.
    [HarmonyPrefix]
    private static bool Prefix(Ship ship) => !CoopShipSnapshotRegistry.IsSnapshotHere(ship, out _);
}

// Mission damage and condition sync write a snapshot's hit points, which the campaign ship sync would log as a client
// write to a managed ship on every hit. Registered campaign ships keep the sync behaviour.
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class SnapshotShipHitPointsSyncScopePatch
{
    // The two hit point setters a snapshot's mission damage and condition sync write.
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.PropertySetter(typeof(Ship), nameof(Ship.HitPoints)),
        AccessTools.PropertySetter(typeof(Ship), nameof(Ship.SailHitPoints)),
    };

    // Opens the allowed scope for a snapshot's write. First, so the AutoSync setter prefix (default priority) runs inside it.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Ship __instance, out IDisposable __state)
    {
        __state = null;
        if (!CoopShipSnapshotRegistry.IsSnapshotHere(__instance, out _)) return;

        __state = CallOriginalPolicy.AllowOriginalsForCurrentOperation();
    }

    // Closes the scope; finalizers run after every postfix and on a throw, so the scope covers the setter's postfixes.
    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
