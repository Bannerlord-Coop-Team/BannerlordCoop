using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatch]
internal class CaravanAmbushPatches
{
    [HarmonyPatch(typeof(CaravanAmbushIssueBehavior), nameof(CaravanAmbushIssueBehavior.OnCheckForIssue))]
    [HarmonyPrefix]
    private static bool Generate() => ModInformation.IsServer;

    [HarmonyPatch(typeof(CaravanAmbushIssueBehavior), nameof(CaravanAmbushIssueBehavior.OnGameLoadFinished))]
    [HarmonyPrefix]
    private static bool LoadBehavior() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Issue), nameof(Issue.GenerateIssueQuest))]
    [HarmonyPostfix]
    private static void QuestId(string questId, QuestBase __result) => __result.StringId = questId;

    [HarmonyPatch(typeof(Quest), nameof(Quest.SetDialogs))]
    [HarmonyPrefix]
    private static bool Dialogs() => ModInformation.IsClient;

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.StartQuest))]
    [HarmonyPrefix]
    private static bool Start(QuestBase __instance) => !(__instance is Quest) || ModInformation.IsClient;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnQuestAccepted))]
    [HarmonyPrefix]
    private static bool Accepted(Quest __instance)
    {
        if (ModInformation.IsServer) return true;
        __instance.StartQuest();
        if (__instance.JournalEntries.Count == 0) __instance.AddLog(__instance.CaravanAmbushIssueQuestActivatedLogText);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.GetCaravaneerDialogFlow))]
    [HarmonyPrefix]
    private static bool CaravanDialog(Quest __instance, ref DialogFlow __result)
    {
        __result = DialogFlow.CreateDialogFlow("start", 125)
            .NpcLine("{=LqEdr7sQ}Thank you, {?PLAYER.GENDER}milady{?}sir{\\?}. {QUEST_GIVER.LINK} had informed me that help would be on the way. We needed it, I think. Those were a pretty tough lot.")
            .Condition(() =>
            {
                StringHelpers.SetCharacterProperties("QUEST_GIVER", __instance.QuestGiver.CharacterObject);
                return __instance._caravanParty != null && __instance._isCaravanSaved &&
                    CharacterObject.OneToOneConversationCharacter == ConversationHelper.GetConversationCharacterPartyLeader(__instance._caravanParty.Party);
            })
            .PlayerLine("{=MKbLhn9d}I'm glad we caught up to you in time.")
            .NpcLine("{=yxg91L0a}We'll tell everyone what you did.[if:convo_happy][ib:normal2] Please take some of these goods in compensation. We have no intention to sell them anyway. Safe travels, {?PLAYER.GENDER}milady{?}sir{\\?}.")
            .Consequence(__instance.OnQuestSucceeded)
            .CloseDialog();
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnQuestSucceeded))]
    [HarmonyPrefix]
    private static bool Reward(Quest __instance)
    {
        if (!__instance.IsOngoing) return false;
        return ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) &&
            dispatcher.BeforeQuestBranch(__instance, (byte)CaravanAmbushBranch.CollectReward);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.HandlePartyAiAfterCompletion))]
    [HarmonyPrefix]
    private static bool Release(Quest __instance)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushEvents>(out var events)) events.Release(__instance);
        return false;
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.FinalizeQuest))]
    [HarmonyPrefix]
    private static void Finalize(QuestBase __instance)
    {
        if (ModInformation.IsClient || !(__instance is Quest quest)) return;
        if (ContainerProvider.TryResolve<ICaravanAmbushStatePublisher>(out var publisher)) publisher.Publish(quest);
        if (ContainerProvider.TryResolve<ICaravanAmbushEvents>(out var events)) events.Release(quest, completing: true);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalized(IssueBase __instance)
    {
        // The framework dismisses a disconnected owner's issue without finalizing its quest.
        if (ModInformation.IsServer && __instance.IssueQuest is Quest quest &&
            ContainerProvider.TryResolve<ICaravanAmbushEvents>(out var events)) events.Release(quest, completing: true);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.InitializeQuestOnGameLoad))]
    [HarmonyPrefix]
    private static bool LoadQuest(Quest __instance)
    {
        if (ModInformation.IsClient)
        {
            __instance.SetDialogs();
            return false;
        }

        return true;
    }
}

[HarmonyPatch]
internal class CaravanAmbushServerListeners
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.HourlyTick));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.MapEventStarted));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.MapEventEnded));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnSettlementEntered));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnBanditPartyRecruited));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared), new[] { typeof(IFaction), typeof(IFaction), typeof(TaleWorlds.CampaignSystem.Actions.DeclareWarAction.DeclareWarDetail) });
    }

    private static bool Prefix() => ModInformation.IsServer;
}
