using Common;
using Common.Logging;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>Routes the three inline dialogue rewards before any personal or world consequence runs.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ExtortionByDesertersBranchPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ExtortionByDesertersBranchPatches>();

    [HarmonyPatch]
    [HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
    private static class FullRewardBranchPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => FindBranch(nameof(Quest._questResultSuccess1));
        [HarmonyPrepare]
        private static bool Prepare() => HasSingleBranch(nameof(Quest._questResultSuccess1));

        [HarmonyPrefix]
        private static bool Prefix(Quest __instance) => BeforeBranch(__instance, ExtortionByDesertersBranch.FullReward);
    }

    [HarmonyPatch]
    [HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
    private static class HalfRewardBranchPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => FindBranch(nameof(Quest._questResultSuccess2));
        [HarmonyPrepare]
        private static bool Prepare() => HasSingleBranch(nameof(Quest._questResultSuccess2));

        [HarmonyPrefix]
        private static bool Prefix(Quest __instance) => BeforeBranch(__instance, ExtortionByDesertersBranch.HalfReward);
    }

    [HarmonyPatch]
    [HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
    private static class RefuseRewardBranchPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => FindBranch(nameof(Quest._questResultSuccess3));
        [HarmonyPrepare]
        private static bool Prepare() => HasSingleBranch(nameof(Quest._questResultSuccess3));

        [HarmonyPrefix]
        private static bool Prefix(Quest __instance) => BeforeBranch(__instance, ExtortionByDesertersBranch.RefuseReward);
    }

    private static bool BeforeBranch(Quest quest, ExtortionByDesertersBranch branch)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }

    private static bool HasSingleBranch(string resultProperty)
    {
        var matches = FindBranches(resultProperty);
        if (matches.Length != 1)
        {
            Logger.Warning("Expected one extortion reward branch for {resultProperty}, found {count}", resultProperty, matches.Length);
            return false;
        }

        return true;
    }

    private static MethodBase FindBranch(string resultProperty) => FindBranches(resultProperty).SingleOrDefault();

    private static MethodInfo[] FindBranches(string resultProperty)
    {
        var getter = AccessTools.PropertyGetter(typeof(Quest), resultProperty);
        if (getter == null) return Array.Empty<MethodInfo>();

        // The consequences are unnamed lambdas that call the getter for their reward result.
        return AccessTools.GetDeclaredMethods(typeof(Quest))
            .Where(method => method.Name.StartsWith("<get_QuestCompletionDialogFlow>b__", StringComparison.Ordinal) &&
                method.ReturnType == typeof(void) && method.GetParameters().Length == 0)
            .Where(method => PatchProcessor.GetOriginalInstructions(method).Any(instruction =>
                (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                Equals(instruction.operand, getter)))
            .ToArray();
    }
}
