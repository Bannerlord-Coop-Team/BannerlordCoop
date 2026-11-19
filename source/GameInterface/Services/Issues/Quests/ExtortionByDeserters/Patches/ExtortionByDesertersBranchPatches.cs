using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
        // The unnamed dialogue consequences call the getter for their reward result.
        var candidates = AccessTools.GetDeclaredMethods(typeof(Quest))
            .Where(method => method.Name.StartsWith("<get_QuestCompletionDialogFlow>b__", StringComparison.Ordinal) &&
                method.ReturnType == typeof(void) && method.GetParameters().Length == 0)
            .Select(method => (Method: method, Calls: PatchProcessor.GetOriginalInstructions(method)
                .Where(instruction => instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                .Select(instruction => instruction.operand)
                .OfType<MethodInfo>().ToArray()))
            .ToArray();

        var bindings = new List<(MethodInfo Method, ExtortionByDesertersBranch Branch)>();
        foreach (var (resultProperty, branch) in new[]
        {
            (nameof(Quest._questResultSuccess1), ExtortionByDesertersBranch.FullReward),
            (nameof(Quest._questResultSuccess2), ExtortionByDesertersBranch.HalfReward),
            (nameof(Quest._questResultSuccess3), ExtortionByDesertersBranch.RefuseReward),
        })
        {
            var getter = AccessTools.PropertyGetter(typeof(Quest), resultProperty);
            var matches = candidates.Where(candidate => candidate.Calls.Contains(getter)).ToArray();
            if (matches.Length != 1)
            {
                Logger.Warning("Expected one extortion reward branch for {resultProperty}, found {count}", resultProperty, matches.Length);
                continue;
            }

            bindings.Add((matches[0].Method, branch));
        }

        var branches = new Dictionary<MethodBase, ExtortionByDesertersBranch>();
        foreach (var group in bindings.GroupBy(binding => binding.Method))
        {
            var matches = group.ToArray();
            if (matches.Length == 1)
            {
                branches.Add(group.Key, matches[0].Branch);
            }
            else
            {
                Logger.Warning("Extortion reward consequence {method} matches more than one branch", group.Key.Name);
            }
        }

        return branches;
    }
}
