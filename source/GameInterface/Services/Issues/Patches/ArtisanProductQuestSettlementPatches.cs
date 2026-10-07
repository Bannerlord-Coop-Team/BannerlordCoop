using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

[HarmonyPatch]
internal sealed class ArtisanProductQuestSettlementPatches
{
    [HarmonyTargetMethods]
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(Quest), "PlayerStartsQuestLogText");
        yield return AccessTools.Method(typeof(Quest), "GetCounterOfferDialogFlow");
        yield return AccessTools.Method(typeof(Quest), "QuestFailedWithRefusal");
        yield return AccessTools.Method(typeof(Quest), "RefuseCounterOfferConsequences");
        yield return AccessTools.Method(typeof(Quest), "OnClanChangedKingdom");
        yield return AccessTools.Method(typeof(Quest), "OnFailed");
        yield return AccessTools.Method(typeof(Quest), "OnCompleteWithSuccess");
    }

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var giver = AccessTools.PropertyGetter(typeof(QuestBase), nameof(QuestBase.QuestGiver));
        var settlement = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.CurrentSettlement));
        var currentOwner = AccessTools.Method(typeof(ArtisanProductQuestSettlementPatches), nameof(GetSettlementOwner));
        var replacements = 0;
        for (var i = 0; i + 1 < code.Count; i++)
        {
            if (!code[i].Calls(giver) || !code[i + 1].Calls(settlement)) continue;
            code[i].opcode = OpCodes.Call;
            code[i].operand = currentOwner;
            replacements++;
        }
        if (replacements == 0) throw new InvalidOperationException("Artisan quest settlement lookup changed.");
        return code;
    }

    // Inheritance keeps QuestGiver, but death clears that hero's current settlement.
    internal static Hero GetSettlementOwner(QuestBase quest)
        => IssueManager.GetIssueOfQuest(quest)?.IssueOwner ?? quest.QuestGiver;
}
