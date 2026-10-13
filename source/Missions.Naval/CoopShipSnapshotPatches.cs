using GameInterface.Policies;
using HarmonyLib;
using NavalDLC.CharacterDevelopment;
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
    [HarmonyPrefix]
    private static bool Prefix(Ship ship) => !CoopShipSnapshots.Contains(ship);
}

// Mission damage to a snapshot is not campaign ship damage, so it grants its owner party no Boatswain XP.
[HarmonyPatch(typeof(NavalSkillLevellingManager), nameof(NavalSkillLevellingManager.OnShipDamaged))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class SnapshotShipDamageXpPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Ship ship) => !CoopShipSnapshots.Contains(ship);
}

// Mission damage and condition sync write a snapshot's hit points, which the campaign ship sync would log as a client
// write to a managed ship on every hit. Registered campaign ships keep the sync behaviour.
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class SnapshotShipHitPointsSyncScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.PropertySetter(typeof(Ship), nameof(Ship.HitPoints)),
        AccessTools.PropertySetter(typeof(Ship), nameof(Ship.SailHitPoints)),
    };

    // First, so the AutoSync setter prefix (default priority) already runs inside the scope.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Ship __instance, out IDisposable __state)
    {
        __state = CoopShipSnapshots.Contains(__instance) ? CallOriginalPolicy.AllowOriginalsForCurrentOperation() : null;
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
