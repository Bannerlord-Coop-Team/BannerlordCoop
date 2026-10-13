using GameInterface.Policies;
using HarmonyLib;
using NavalDLC.CharacterDevelopment;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
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
    [ThreadStatic]
    private static int snapshotWrites;

    /// <summary>True while a snapshot's hit point setter, its postfixes included, runs on this thread.</summary>
    internal static bool IsWritingSnapshot => snapshotWrites > 0;

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
        __state = null;
        if (!CoopShipSnapshots.Contains(__instance)) return;

        snapshotWrites++;
        __state = CallOriginalPolicy.AllowOriginalsForCurrentOperation();
    }

    // Finalizers run after every postfix, so the flag covers the campaign postfixes on the setter.
    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state)
    {
        if (__state == null) return;

        snapshotWrites--;
        __state.Dispose();
    }
}

// The campaign HitPoints postfix (ShipHitPointsPatches) refreshes the open menu for main-party ships; a snapshot is no
// campaign ship, and while a mission opens or runs that menu context can have no handler.
[HarmonyPatch(typeof(GameMenuManager), nameof(GameMenuManager.RefreshMenuOptions))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class SnapshotShipMenuRefreshPatch
{
    [HarmonyPrefix]
    internal static bool Prefix() => !SnapshotShipHitPointsSyncScopePatch.IsWritingSnapshot;
}
