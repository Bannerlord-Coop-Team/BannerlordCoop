using Common;
using Common.Logging;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private static MethodBase TargetMethod() => FindBranch(nameof(Quest._questResultSuccess1));
        private static bool Prepare() => HasSingleBranch(nameof(Quest._questResultSuccess1));

        [HarmonyPrefix]
        private static bool Prefix(Quest __instance) => BeforeBranch(__instance, ExtortionByDesertersBranch.FullReward);
    }

    [HarmonyPatch]
    [HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
    private static class HalfRewardBranchPatch
    {
        private static MethodBase TargetMethod() => FindBranch(nameof(Quest._questResultSuccess2));
        private static bool Prepare() => HasSingleBranch(nameof(Quest._questResultSuccess2));

        [HarmonyPrefix]
        private static bool Prefix(Quest __instance) => BeforeBranch(__instance, ExtortionByDesertersBranch.HalfReward);
    }

    [HarmonyPatch]
    [HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
    private static class RefuseRewardBranchPatch
    {
        private static MethodBase TargetMethod() => FindBranch(nameof(Quest._questResultSuccess3));
        private static bool Prepare() => HasSingleBranch(nameof(Quest._questResultSuccess3));

        [HarmonyPrefix]
        private static bool Prefix(Quest __instance) => BeforeBranch(__instance, ExtortionByDesertersBranch.RefuseReward);
    }

    private static bool BeforeBranch(Quest quest, ExtortionByDesertersBranch branch)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }

    private static bool HasSingleBranch(string resultField)
    {
        var matches = FindBranches(resultField).ToList();
        if (matches.Count != 1)
        {
            Logger.Warning("Expected one extortion reward branch for {resultField}, found {count}", resultField, matches.Count);
            return false;
        }

        return true;
    }

    private static MethodBase FindBranch(string resultField) => FindBranches(resultField).SingleOrDefault();

    private static IEnumerable<MethodInfo> FindBranches(string resultField)
    {
        var field = AccessTools.Field(typeof(Quest), resultField);
        if (field == null) yield break;

        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Quest)).Where(method =>
                     method.Name.StartsWith("<get_QuestCompletionDialogFlow>b__", StringComparison.Ordinal) &&
                     method.ReturnType == typeof(void) && method.GetParameters().Length == 0))
        {
            var body = method.GetMethodBody()?.GetILAsByteArray();
            if (body == null) continue;
            for (var offset = 0; offset <= body.Length - 5; offset++)
            {
                var opcode = body[offset];
                if (opcode != 0x7B && opcode != 0x7E) continue;
                var token = BitConverter.ToInt32(body, offset + 1);
                if (method.Module.ResolveField(token) == field)
                {
                    yield return method;
                    break;
                }
            }
        }
    }
}
