using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CheckPreconditions))]
internal static class HeadmanHerdEligibilityPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var issues = AccessTools.Field(typeof(IssueManager), nameof(IssueManager.Issues));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode != OpCodes.Ldfld || !Equals(instruction.operand, issues)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(HeadmanHerdEligibilityPatch), nameof(FilterIssues)));
        }
        if (matches != 1) throw new InvalidOperationException("Issue precondition duplicate scan changed");
    }

    internal static MBReadOnlyDictionary<Hero, IssueBase> FilterIssues(MBReadOnlyDictionary<Hero, IssueBase> issues, IssueBase issue)
    {
        if (issue is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()
            || !ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership)) return issues;
        return new MBReadOnlyDictionary<Hero, IssueBase>(issues.Where(entry => ownership.IsVisibleToCurrentPlayer(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value));
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionCondition))]
internal static class HeadmanHerdAlternativeEligibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Issue __instance, ref bool __result, ref TextObject explanation)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (__instance.CheckPreconditions(__instance.IssueOwner, out explanation)) return true;
        __result = false;
        return false;
    }
}
