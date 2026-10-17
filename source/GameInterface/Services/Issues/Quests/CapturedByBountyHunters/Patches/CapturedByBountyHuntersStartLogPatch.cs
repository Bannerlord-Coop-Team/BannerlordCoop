using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters.Patches;

/// <summary>
/// Vanilla's start log names the settlement the player is in. When the offer conversation is opened outside a
/// settlement there is none and accepting throws, so the log names the giver's settlement instead.
/// </summary>
[HarmonyPatch(typeof(Quest), nameof(Quest.PlayerStartsQuestLogText), MethodType.Getter)]
internal class CapturedByBountyHuntersStartLogPatch
{
    private static readonly MethodInfo CurrentSettlementGetter = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.CurrentSettlement));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var replaced = 0;

        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(CurrentSettlementGetter))
            {
                yield return instruction;
                continue;
            }

            yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
            yield return CodeInstruction.Call(typeof(CapturedByBountyHuntersStartLogPatch), nameof(SettlementOf));
            replaced++;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException("The bounty hunters start log no longer reads the current settlement once");
        }
    }

    private static Settlement SettlementOf(Quest quest)
    {
        return Settlement.CurrentSettlement ?? quest.QuestGiver.CurrentSettlement;
    }
}
