using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>Routes the three inline dialogue rewards before any personal or world consequence runs.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ExtortionByDesertersBranchPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ExtortionByDesertersBranchPatches>();

    private static readonly Lazy<IReadOnlyDictionary<MethodBase, ExtortionByDesertersBranch>> Branches =
        new(FindBranches);

    [HarmonyPrepare]
    private static bool Prepare() => Branches.Value.Count > 0;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods() => Branches.Value.Keys;

    [HarmonyPrefix]
    private static bool Prefix(Quest __instance, MethodBase __originalMethod)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(__instance, (byte)Branches.Value[__originalMethod]);
    }

    private static IReadOnlyDictionary<MethodBase, ExtortionByDesertersBranch> FindBranches()
    {
        var branches = new Dictionary<MethodBase, ExtortionByDesertersBranch>();
        var seenConsequences = new HashSet<MethodBase>();
        foreach (var (resultProperty, branch) in new[]
        {
            (nameof(Quest._questResultSuccess1), ExtortionByDesertersBranch.FullReward),
            (nameof(Quest._questResultSuccess2), ExtortionByDesertersBranch.HalfReward),
            (nameof(Quest._questResultSuccess3), ExtortionByDesertersBranch.RefuseReward),
        })
        {
            var getter = AccessTools.PropertyGetter(typeof(Quest), resultProperty);
            if (getter == null)
            {
                Logger.Error("Extortion reward routing is not installed for {resultProperty}: the result getter is missing",
                    resultProperty);
                continue;
            }

            var consequence = FindConsequenceCalling(getter);
            if (consequence == null) continue;
            if (!seenConsequences.Add(consequence))
            {
                Logger.Error("Extortion reward routing is not installed for {method}: the consequence matches more than one branch",
                    consequence.Name);
                branches.Remove(consequence);
                continue;
            }

            branches.Add(consequence, branch);
        }

        return branches;
    }

    private static MethodInfo FindConsequenceCalling(MethodInfo getter)
    {
        // The unnamed dialogue consequences call the getter for their reward result.
        var matches = AccessTools.GetDeclaredMethods(typeof(Quest))
            .Where(method => method.Name.StartsWith("<get_QuestCompletionDialogFlow>b__", StringComparison.Ordinal) &&
                method.ReturnType == typeof(void) && method.GetParameters().Length == 0)
            .Where(method => PatchProcessor.GetOriginalInstructions(method).Any(instruction => instruction.Calls(getter)))
            .ToArray();

        if (matches.Length == 1) return matches[0];

        Logger.Error("Extortion reward routing is not installed for {getter}: expected one consequence, found {count}",
            getter.Name, matches.Length);
        return null;
    }
}
