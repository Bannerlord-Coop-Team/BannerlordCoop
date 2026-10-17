using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools.Patches;

/// <summary>Routes delivery to the server and keeps replay from applying personal penalties twice.</summary>
[HarmonyPatch]
internal class VillageNeedsToolsOutcomePatches
{
    [HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionStartConsequence))]
    [HarmonyPrefix]
    private static bool FundingPrefix(Issue __instance)
    {
        if (ModInformation.IsClient) return false;

        if (Hero.MainHero.Gold < __instance.CostOfToolsForAlternativeSolution)
        {
            throw new InvalidOperationException("The owner can no longer afford the tools");
        }

        return true;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.FinishQuestSuccess1))]
    [HarmonyPrefix]
    private static bool FinishQuestSuccessPrefix(Quest __instance)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)
            || dispatcher.BeforeQuestBranch(__instance, (byte)VillageNeedsToolsBranch.DeliverTools);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFailed))]
    [HarmonyPrefix]
    private static bool PenaltyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            // Run the timeout's mutations through the current sync patches.
            __instance.AddLog(__instance.QuestTimeOutFailLog);
            __instance.QuestGiver.AddPower(-10f);
            __instance.QuestGiver.CurrentSettlement.Village.Hearth -= 30f;
            ChangeRelationAction.ApplyPlayerRelation(__instance.QuestGiver, -5);
        }

        return false;
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
    [HarmonyPostfix]
    private static void JournalPostfix(QuestBase __instance, TextObject text)
    {
        if (ModInformation.IsServer && __instance is Quest quest && text != null)
        {
            MessageBroker.Instance.Publish(quest, new VillageNeedsToolsJournalAdded(quest, text.ToString()));
        }
    }
}
