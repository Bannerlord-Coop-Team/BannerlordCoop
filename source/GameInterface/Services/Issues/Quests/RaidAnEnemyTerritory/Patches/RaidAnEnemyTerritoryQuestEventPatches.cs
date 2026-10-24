using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal sealed class RaidAnEnemyTerritoryQuestEventPatches
{
    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool IssueManagerOnHeroKilledPrefix(
        Hero victim,
        Hero killer,
        KillCharacterAction.KillCharacterActionDetail detail,
        bool showNotification = true)
    {
        if (ModInformation.IsClient || victim?.Issue?.IssueQuest is not Quest)
        {
            return true;
        }

        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver) ||
            !ownerResolver.TryResolveOwnerHero(victim.Issue, out _))
        {
            return true;
        }

        RunAsOwners(quest => quest.OnHeroKilled(victim, killer, detail, showNotification));
        return false;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementOwnerChanged))]
    [HarmonyPostfix]
    private static void OnSettlementOwnerChangedPostfix(object[] __args) =>
        RunAsOwners(quest => quest.OnSettlementOwnerChanged(
            (Settlement)__args[0], (bool)__args[1], (Hero)__args[2], (Hero)__args[3],
            (Hero)__args[4], (ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail)__args[5]));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMakePeace))]
    [HarmonyPostfix]
    private static void OnMakePeacePostfix(object[] __args) =>
        RunAsOwners(quest => quest.OnMakePeace(
            (IFaction)__args[0], (IFaction)__args[1], (MakePeaceAction.MakePeaceDetail)__args[2]));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.DailyTick))]
    [HarmonyPostfix]
    private static void DailyTickPostfix()
    {
        RunAsOwners(quest =>
        {
            quest.DailyTick();
            if (quest.IsOngoing && quest.QuestDueTime.IsPast)
            {
                quest.CompleteQuestWithTimeOut();
            }
        });
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool OnHeroKilledPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMakePeace))]
    [HarmonyPrefix]
    private static bool OnMakePeacePrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool OnClanChangedKingdomPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.DailyTick))]
    [HarmonyPrefix]
    private static bool DailyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnBeforeTimedOut))]
    [HarmonyPrefix]
    private static bool OnBeforeTimedOutPrefix() => ModInformation.IsServer;

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners(listener);
    }
}
