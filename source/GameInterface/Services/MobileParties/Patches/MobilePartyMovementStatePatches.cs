using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal static class MobilePartyMovementStatePatches
{
    [ThreadStatic]
    private static int movementCommandDepth;

    private static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools.GetDeclaredMethods(typeof(MobileParty))
            .Where(method => method.Name.StartsWith("SetMove", StringComparison.Ordinal) ||
                method.Name == nameof(MobileParty.RecalculateShortTermBehavior) ||
                method.Name == nameof(MobileParty.SetShortTermBehavior) ||
                method.Name == nameof(MobileParty.SetTargetSettlement));

    [HarmonyPrefix]
    private static void Prefix(out bool __state) =>
        __state = movementCommandDepth++ == 0 &&
            ModInformation.IsServer &&
            !CallOriginalPolicy.IsOriginalAllowed();

    [HarmonyFinalizer]
    private static Exception Finalizer(
        MobileParty __instance,
        MethodBase __originalMethod,
        bool __state,
        Exception __exception)
    {
        movementCommandDepth--;

        if (__exception == null &&
            movementCommandDepth == 0 &&
            __originalMethod.Name == nameof(MobileParty.SetMoveModeHold) &&
            !CallOriginalPolicy.IsOriginalAllowed())
            __instance.SetNavigationModeHold();

        if (__exception == null && __state && __instance?.IsActive == true)
            MessageBroker.Instance.Publish(__instance, new PartyBehaviorChangeAttempted(__instance));

        return __exception;
    }
}
