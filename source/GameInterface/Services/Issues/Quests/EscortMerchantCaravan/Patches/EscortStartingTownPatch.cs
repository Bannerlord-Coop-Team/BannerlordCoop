using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.PlayerStartsQuestLogText), MethodType.Getter)]
internal class EscortStartingTownPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.CurrentSettlement));
        var town = AccessTools.Method(typeof(EscortStartingTownPatch), nameof(GiverTown));
        var count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                count++;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                instruction.opcode = OpCodes.Call;
                instruction.operand = town;
            }
            yield return instruction;
        }
        if (count != 1) throw new InvalidOperationException("Escort starting-town call site changed.");
    }

    private static Settlement GiverTown(Quest quest) => quest.QuestGiver.CurrentSettlement;
}
