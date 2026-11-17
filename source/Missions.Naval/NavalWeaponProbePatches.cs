#if DEBUG
using HarmonyLib;
using Missions.Messages;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Observes successful replay without modifying its effects or return value.</summary>
[HarmonyPatch(typeof(NavalShipEngine), nameof(NavalShipEngine.ReplayWeaponShot))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalWeaponReplayProbePatch
{
    /// <summary>The completion event follows any fire animation emitted inside the replay call.</summary>
    private static void Postfix(MissionObject weapon, bool playLaunchEffects, bool __result)
    {
        if (__result) Mission.Current?.GetMissionBehavior<NavalWeaponProbe>()?.Shot(weapon, playLaunchEffects);
    }
}

/// <summary>Observes the integer overload once, including calls routed from the string overload.</summary>
[HarmonyPatch(typeof(SynchedMissionObject), nameof(SynchedMissionObject.SetAnimationAtChannelSynched), new[] { typeof(int), typeof(int), typeof(float) })]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalWeaponAnimationProbePatch
{
    /// <summary>Records channel zero only after the animation call completes.</summary>
    private static void Postfix(SynchedMissionObject __instance, int animationIndex, int channelNo)
    {
        if (channelNo == 0) Mission.Current?.GetMissionBehavior<NavalWeaponProbe>()?.Animation(__instance, animationIndex);
    }
}

/// <summary>Observes reload animations resumed by replicated state changes.</summary>
[HarmonyPatch(typeof(SynchedMissionObject), nameof(SynchedMissionObject.ResumeSkeletonAnimationSynched))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalWeaponResumeProbePatch
{
    /// <summary>Records the current channel so offline analysis can distinguish resume from a new animation.</summary>
    private static void Postfix(SynchedMissionObject __instance) => Mission.Current?.GetMissionBehavior<NavalWeaponProbe>()?.Animation(__instance, null);
}
/// <summary>Observes replicated reload states after they have been applied to a copy.</summary>
[HarmonyPatch(typeof(NavalShipEngine), nameof(NavalShipEngine.ApplyWeaponState))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalWeaponStateProbePatch
{
    /// <summary>Records the received state without assigning the copy's simulator State.</summary>
    private static void Postfix(MissionObject weapon, BattleShipWeaponState state) =>
        Mission.Current?.GetMissionBehavior<NavalWeaponProbe>()?.AppliedState(weapon, state);
}
#endif
