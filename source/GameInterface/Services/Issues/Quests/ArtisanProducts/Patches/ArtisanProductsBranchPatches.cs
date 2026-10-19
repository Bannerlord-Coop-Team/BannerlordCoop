using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts.Patches;

[HarmonyPatch]
internal class ArtisanProductsBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.DeliverItemsPartiallyOnConsequence))]
    [HarmonyPrefix]
    private static bool Partial(Quest __instance) => Before(__instance, ArtisanProductsBranch.PartialDelivery);

    [HarmonyPatch(typeof(Quest), nameof(Quest.DeliverItemsFullyOnConsequence))]
    [HarmonyPrefix]
    private static bool Full(Quest __instance) => Before(__instance, ArtisanProductsBranch.FullDelivery);

    [HarmonyPatch(typeof(Quest), nameof(Quest.DeliverItemsRejectOnConsequence))]
    [HarmonyPrefix]
    private static bool Reject(Quest __instance) => Before(__instance, ArtisanProductsBranch.RefuseDelivery);

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestFailedWithRefusal))]
    [HarmonyPrefix]
    private static bool Surrender(Quest __instance) => Before(__instance, ArtisanProductsBranch.SurrenderGoods);

    [HarmonyPatch(typeof(Quest), nameof(Quest.RefuseCounterOfferConsequences))]
    [HarmonyPrefix]
    private static bool Refuse(Quest __instance) => Before(__instance, ArtisanProductsBranch.RefuseMerchant);

    private static bool Before(Quest quest, ArtisanProductsBranch branch)
        => !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) || dispatcher.BeforeQuestBranch(quest, (byte)branch);

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Accept(IEnumerable<CodeInstruction> instructions)
    {
        var add = AccessTools.Method(typeof(ItemRoster), nameof(ItemRoster.AddToCounts), new[] { typeof(ItemObject), typeof(int) });
        var replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(add))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(ArtisanProductsBranchPatches), nameof(GrantGoods));
                replaced++;
            }
            yield return instruction;
        }
        if (replaced != 1) throw new InvalidOperationException("Artisan quest accept item grant changed");
    }

    // Acceptance grants the goods once on the server through the accept strategy.
    private static int GrantGoods(ItemRoster roster, ItemObject item, int count) => -1;
}

[HarmonyPatch]
internal class ArtisanProductsCompletionProgressPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnFailed));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnCompleteWithSuccess));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut));
    }

    [HarmonyPrefix]
    private static bool Before() => ModInformation.IsServer;

    [HarmonyPostfix]
    private static void After(Quest __instance)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IArtisanProductsProgress>(out var progress)) progress.Send(__instance);
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
internal class ArtisanProductsJournalPatch
{
    [HarmonyPostfix]
    private static void After(QuestBase __instance)
    {
        if (ModInformation.IsServer && __instance is Quest quest && quest.IsOngoing &&
            ContainerProvider.TryResolve<IArtisanProductsProgress>(out var progress)) progress.Send(quest);
    }
}
