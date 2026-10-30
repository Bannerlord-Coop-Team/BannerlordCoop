using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Vanilla gives MobileParty.MainParty its own embark and disembark checks and land to sea path cost.
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
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.GetRegionSwitchCostFromLandToSea)),
    };

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => ReplaceIsMainPartyWithIsPlayerParty(instructions);

    internal static IEnumerable<CodeInstruction> ReplaceIsMainPartyWithIsPlayerParty(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            // Both take the party on the stack and return a bool
            if (instruction.Calls(IsMainPartyGetter))
            {
                yield return new CodeInstruction(OpCodes.Call, IsPlayerPartyMethod).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                continue;
            }

            yield return instruction;
        }
    }
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

[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.ComputePath))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PlayerLandSeaPathPatch
{
    private static readonly MethodInfo MainPartyGetter = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.MainParty));

    // Vanilla checks IsMainParty && MainParty.IsCurrentlyAtSea, so the second check also has to read this party
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in PlayerLandSeaTransitionPatches.ReplaceIsMainPartyWithIsPlayerParty(instructions))
        {
            if (instruction.Calls(MainPartyGetter))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                continue;
            }

            yield return instruction;
        }
    }
}
