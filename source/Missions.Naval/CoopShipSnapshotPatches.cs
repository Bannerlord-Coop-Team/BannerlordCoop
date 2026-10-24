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
    // Skips the destroy for a snapshot; campaign ships run it.
    [HarmonyPrefix]
    private static bool Prefix(Ship ship) => !CoopShipSnapshots.Contains(ship);
}

// Mission damage to a snapshot is not campaign ship damage, so it grants no Boatswain XP here; the mitigated part is summed and
// reported with the ship's fate, and the server grants it once (vanilla: 0.1 XP per point of rawDamage - finalDamage).
[HarmonyPatch(typeof(NavalSkillLevellingManager), nameof(NavalSkillLevellingManager.OnShipDamaged))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class SnapshotShipDamageXpPatch
{
    // Sums a snapshot's mitigated damage instead of granting XP; campaign ships run the vanilla grant.
    [HarmonyPrefix]
    internal static bool Prefix(Ship ship, float rawDamage, float finalDamage)
    {
        if (!CoopShipSnapshots.Contains(ship)) return true;

        CoopShipSnapshots.AddMitigatedDamage(ship, Math.Max(rawDamage - finalDamage, 0f));
        return false;
    }
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
        if (!CoopShipSnapshots.Contains(__instance)) return;

        __state = CallOriginalPolicy.AllowOriginalsForCurrentOperation();
    }

    // Closes the scope; finalizers run after every postfix and on a throw, so the scope covers the setter's postfixes.
    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
