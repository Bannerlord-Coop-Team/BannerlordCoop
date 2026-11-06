using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Vanilla gives MobileParty.MainParty its own embark and disembark checks.
/// Every machine checks IsPlayerParty instead, so the server and other clients move a player's party the way its owner does.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PlayerLandSeaTransitionPatches
{
    private static readonly MethodInfo IsMainPartyGetter = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.IsMainParty));
    private static readonly MethodInfo IsPlayerPartyMethod = AccessTools.Method(typeof(MobilePartyExtensions), nameof(MobilePartyExtensions.IsPlayerParty));

    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.CheckTransitionParallel)),
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.CommonTransitioningPartyTick)),
    };

    // Both take the party on the stack and return a bool
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => instructions.MethodReplacer(IsMainPartyGetter, IsPlayerPartyMethod);
}

[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.FinishNavigationTransitionInternal))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class LandSeaTransitionFinishPatch
{
    // Each side flips IsCurrentlyAtSea itself when its transition ends, and a client may not run the transition at all,
    // so the server sends its result for every party to stop them drifting apart
    [HarmonyPostfix]
    private static void Postfix(MobileParty __instance)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new PartyBehaviorChangeAttempted(__instance, forcePosition: true, isCurrentlyAtSea: __instance.IsCurrentlyAtSea));
    }
}

[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.CommonTransitioningPartyTick))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PlayerLandSeaTransitionCancelPatch
{
    [HarmonyPrefix]
    private static void Prefix(MobileParty __instance, out bool __state)
    {
        __state = __instance.IsTransitionInProgress;
    }

    // A transition the server cancels keeps the old sea state, so correct a client that already finished its own
    [HarmonyPostfix]
    private static void Postfix(MobileParty __instance, bool __state)
    {
        if (ModInformation.IsClient) return;
        if (!__state || __instance.IsTransitionInProgress || !__instance.IsPlayerParty()) return;

        // This runs in the parallel party tick, so publish from the game thread
        GameThread.RunSafe(() =>
            MessageBroker.Instance.Publish(__instance, new PartyBehaviorChangeAttempted(__instance, forcePosition: true, isCurrentlyAtSea: __instance.IsCurrentlyAtSea)),
            context: nameof(PlayerLandSeaTransitionCancelPatch));
    }
}

/// <summary>
/// Vanilla gives MobileParty.MainParty its own land to sea path cost and keeps it on water when its target allows it.
/// Every machine gives a player's party the same, so the server and other clients path it the way its owner does.
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PlayerLandSeaPathPatch
{
    // Gives a player's party the cost vanilla only gives the main party
    [HarmonyPatch(nameof(MobileParty.GetRegionSwitchCostFromLandToSea))]
    [HarmonyPostfix]
    private static void GetRegionSwitchCostFromLandToSeaPostfix(MobileParty __instance, ref int __result)
    {
        if (!__instance.IsPlayerParty()) return;

        __result = Campaign.PlayerRegionSwitchCostFromLandToSea;
    }

    // Same check as vanilla's IsMainParty && MainParty.IsCurrentlyAtSea. Nothing in ComputePath reads navigationType before vanilla's own check.
    [HarmonyPatch(nameof(MobileParty.ComputePath))]
    [HarmonyPrefix]
    private static void ComputePathPrefix(MobileParty __instance, CampaignVec2 newTargetPosition, ref MobileParty.NavigationType navigationType)
    {
        if (!__instance.IsPlayerParty() || !__instance.IsCurrentlyAtSea) return;
        if (!NavigationHelper.IsPositionValidForNavigationType(newTargetPosition, MobileParty.NavigationType.Naval)) return;

        navigationType = MobileParty.NavigationType.Naval;
    }
}
