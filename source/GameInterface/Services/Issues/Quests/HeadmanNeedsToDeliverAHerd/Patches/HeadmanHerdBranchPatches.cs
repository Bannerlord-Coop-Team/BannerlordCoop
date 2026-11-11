using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class HeadmanHerdBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.DeliverHerdOnConsequence))]
    [HarmonyPrefix]
    private static bool DeliverPrefix(Quest __instance) => BeforeBranch(__instance, HeadmanHerdBranch.Deliver);

    [HarmonyPatch(typeof(Quest), nameof(Quest.DeliverHerdRejectOnConsequence))]
    [HarmonyPrefix]
    private static bool KeepPrefix(Quest __instance) => BeforeBranch(__instance, HeadmanHerdBranch.Keep);

    private static bool BeforeBranch(Quest quest, HeadmanHerdBranch branch)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.AddHerdAndShepherdsToMainParty))]
    [HarmonyPrefix]
    private static bool GrantHerdPrefix() => ModInformation.IsServer;

    // The server already replicated the inventory, gold and settlement effects before this replay.
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnCompleteWithSuccess))]
    [HarmonyPrefix]
    private static bool SuccessPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnCompleteWithSuccess))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SuccessTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var enumerator = AccessTools.Method(typeof(ItemRoster), nameof(ItemRoster.GetEnumerator));
        var snapshot = AccessTools.Method(typeof(Enumerable), nameof(Enumerable.ToArray)).MakeGenericMethod(typeof(ItemRosterElement));
        var snapshotEnumerator = AccessTools.Method(typeof(IEnumerable<ItemRosterElement>), nameof(IEnumerable<ItemRosterElement>.GetEnumerator));
        var replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(enumerator))
            {
                // Depleting a stack swaps the last element into its slot, which live enumeration skips.
                yield return new CodeInstruction(OpCodes.Call, snapshot).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                instruction.opcode = OpCodes.Callvirt;
                instruction.operand = snapshotEnumerator;
                replaced++;
            }
            yield return instruction;
        }
        if (replaced != 1) throw new InvalidOperationException("The herd success no longer enumerates the item roster once");
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFailed))]
    [HarmonyPrefix]
    private static bool FailurePrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.RemoveRemainingHorses))]
    [HarmonyPrefix]
    private static bool RemoveRemainingPrefix(Quest __instance)
    {
        if (ModInformation.IsClient) return true;
        if (!ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) return true;
        cleanup.RemoveRemaining(__instance);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnCanceled))]
    [HarmonyPrefix]
    private static bool CanceledPrefix(Quest __instance)
    {
        if (ModInformation.IsServer) __instance.RemoveRemainingHorses();
        return false;
    }
}
