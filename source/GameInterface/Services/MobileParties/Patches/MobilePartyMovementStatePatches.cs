using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Captures one complete movement snapshot after an outermost vanilla movement mutation finishes.
/// Nested helpers are deliberately collapsed so no partially-reset state is published.
/// </summary>
[HarmonyPatch]
internal static class MobilePartyMovementStatePatches
{
    [ThreadStatic]
    private static int movementCommandDepth;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.RecalculateShortTermBehavior));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetShortTermBehavior));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetTargetSettlement));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveModeHold));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveEngageParty));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveGoAroundParty));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveGoToSettlement));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveGoToPoint));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveToNearestLand));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveGoToInteractablePoint));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveEscortParty));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMovePatrolAroundPoint));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMovePatrolAroundSettlement));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveRaidSettlement));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveBesiegeSettlement));
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveDefendSettlement));
    }

    [HarmonyPrefix]
    private static void Prefix(out bool __state)
    {
        __state = movementCommandDepth == 0 &&
            ModInformation.IsServer &&
            !CallOriginalPolicy.IsOriginalAllowed();
        movementCommandDepth++;
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(MobileParty __instance, bool __state, Exception __exception)
    {
        movementCommandDepth = Math.Max(0, movementCommandDepth - 1);

        if (ShouldPublishMovementState(__instance, __state, __exception))
        {
            MessageBroker.Instance.Publish(
                __instance,
                new MobilePartyMovementStateChanged(__instance));
        }

        return __exception;
    }

    internal static bool ShouldPublishMovementState(
        MobileParty party,
        bool isAuthoritativeMutation,
        Exception exception) =>
        exception == null && isAuthoritativeMutation && party?.IsActive == true;
}

/// <summary>
/// Publishes the complete navigation state after the mod's direct hold reset finishes.
/// </summary>
[HarmonyPatch(typeof(MobilePartyExtensions), nameof(MobilePartyExtensions.ResetNavigationToHold))]
internal static class MobilePartyNavigationResetPatches
{
    [HarmonyPostfix]
    private static void Postfix(MobileParty party)
    {
        if (ModInformation.IsClient ||
            CallOriginalPolicy.IsOriginalAllowed() ||
            party?.IsActive != true)
            return;

        GameThread.RunSafe(() => MessageBroker.Instance.Publish(
            party,
            new MobilePartyMovementStateChanged(party)));
    }
}
