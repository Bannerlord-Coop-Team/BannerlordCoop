using Common;
using Common.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
internal class GangLeaderNeedsToOffloadStolenGoodsHideoutRevealPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var setter = AccessTools.PropertySetter(typeof(Settlement), nameof(Settlement.IsVisible));
        var replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(setter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(GangLeaderNeedsToOffloadStolenGoodsHideoutRevealPatch), nameof(Reveal));
                replaced++;
            }
            yield return instruction;
        }
        if (replaced != 1) throw new InvalidOperationException("Stolen goods quest accept hideout reveal changed");
    }

    // The owner's client marks its quest hideout for itself like CapturedByBountyHuntersHideoutRevealPatch,
    // only this call is allowed so StartQuest in the same accept still publishes QuestAcceptanceStarted.
    private static void Reveal(Settlement hideout, bool visible)
    {
        using (ModInformation.IsClient ? new AllowedThread() : null)
        {
            hideout.IsVisible = visible;
        }
    }
}
