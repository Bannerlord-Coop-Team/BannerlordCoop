using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan.Patches;

[HarmonyPatch]
internal class EscortQuestPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyPrefix]
    private static bool Accept(Quest __instance)
    {
        if (ModInformation.IsServer || !__instance.IsOngoing || IssueManager.GetIssueOfQuest(__instance) == null) return false;
        if (!Campaign.Current.QuestManager.Quests.Contains(__instance)) __instance.StartQuest();
        if (__instance._playerStartsQuestLog == null)
            __instance._playerStartsQuestLog = __instance.AddDiscreteLog(__instance.PlayerStartsQuestLogText,
                new TextObject("{=r2y3n7dR}Visited Settlements"), 0, __instance._requiredSettlementNumber);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.SpawnCaravan))]
    [HarmonyPrefix]
    private static bool Spawn() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.conversation_caravan_fight_on_consequence))]
    [HarmonyPrefix]
    private static bool Attack(Quest __instance)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
            dispatcher.BeforeQuestBranch(__instance, (byte)EscortBranch.AttackCaravan);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.caravan_talk_on_condition))]
    [HarmonyPrefix]
    private static bool Talk(Quest __instance, ref bool __result)
    {
        if (__instance.IsOngoing && __instance._questCaravanMobileParty != null) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(PartyBase), nameof(PartyBase.SetAsCameraFollowParty))]
    [HarmonyPrefix]
    private static bool Follow(PartyBase __instance)
    {
        var quest = EscortQuestEventPatches.CurrentQuest;
        if (ModInformation.IsClient || quest == null || __instance != quest._questCaravanMobileParty?.Party) return true;
        if (ContainerProvider.TryResolve<IEscortProgress>(out var progress)) progress.Publish(quest, true);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool War(Quest __instance, IFaction faction1, IFaction faction2,
        TaleWorlds.CampaignSystem.Actions.DeclareWarAction.DeclareWarDetail detail)
    {
        if (ModInformation.IsClient || __instance._questCaravanMobileParty == null) return false;
        var caravan = __instance._questCaravanMobileParty;
        if (detail == TaleWorlds.CampaignSystem.Actions.DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility &&
            (faction1 == caravan.MapFaction || faction2 == caravan.MapFaction) &&
            EscortHostilityPatch.Attacker == TaleWorlds.CampaignSystem.Party.PartyBase.MainParty)
            __instance.FailByPlayerHostileConsequences();
        else
            __instance.CheckWarDeclaration();
        if (__instance.IsOngoing && (caravan.TargetSettlement == null ||
            caravan.TargetSettlement.MapFaction.IsAtWarWith(caravan.MapFaction)))
            __instance.TryToFindAndSetTargetToNextSettlement();
        return false;
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.FinalizeQuest))]
    [HarmonyPrefix]
    private static void FinalJournal(QuestBase __instance)
    {
        if (ModInformation.IsServer && __instance is Quest quest &&
            ContainerProvider.TryResolve<IEscortProgress>(out var progress))
            progress.Publish(quest);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void Dismiss(IssueBase __instance)
    {
        if (ModInformation.IsServer &&
            __instance is EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue issue &&
            issue.IsSolvingWithAlternative && ContainerProvider.TryResolve<IEscortProgress>(out var progress))
            progress.PublishAlternative(issue);
        // The framework dismisses a disconnected player's issue without completing its quest.
        if (ModInformation.IsServer && __instance.IssueQuest is Quest quest && quest.IsOngoing)
        {
            __instance.IssueQuest = null;
            quest.CompleteQuestWithCancel();
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPostfix]
    private static void ReleaseParties(Quest __instance)
    {
        if (ModInformation.IsClient) return;
        __instance._questCaravanMobileParty?.SetPartyUsedByQuest(false);
        foreach (var party in new[] { __instance._questBanditMobileParty, __instance._otherBanditParty })
        {
            if (party?.IsActive != true) continue;
            party.SetPartyUsedByQuest(false);
            party.Ai.SetDoNotMakeNewDecisions(false);
            party.IgnoreByOtherPartiesTill(CampaignTime.Now);
        }
    }
}
