using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch]
public class HeadmanNeedsGrainProsperityPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.Success));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.TimeoutFail));
        yield return AccessTools.Method(typeof(Issue), nameof(Issue.AlternativeSolutionEndWithSuccessConsequence));
        yield return AccessTools.Method(typeof(Issue), nameof(Issue.AlternativeSolutionEndWithFailureConsequence));
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var setter = AccessTools.PropertySetter(typeof(Town), nameof(Town.Prosperity));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(setter))
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HeadmanNeedsGrainProsperityPatches),
                    nameof(SetProsperity))).MoveLabelsFrom(instruction);
            }
            else yield return instruction;
        }
    }

    // Native callers can inline the small setter; AutoSync instruments this field write explicitly.
    public static void SetProsperity(Town town, float value) => town._prosperity = Math.Max(0f, value);
}
