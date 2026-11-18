using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

using Quest = RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.OnCheckForIssue))]
internal class RevenueFarmingOfferAuthorityPatch
{
    private static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.Instance), MethodType.Getter)]
internal class RevenueFarmingInstancePatch
{
    private static bool Prefix(ref Quest __result)
    {
        if (!ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return true;
        __result = execution.Current?.Session.Quest;
        if (__result == null && ModInformation.IsClient && ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions))
            __result = sessions.LocalQuest;
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.SyncData))]
internal class RevenueFarmingSavePatch
{
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions)) sessions.SyncData(dataStore);
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class RevenueFarmingCallbackPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { nameof(Quest.HourlyTick), nameof(Quest.OnWarDeclared), nameof(Quest.OnClanChangedKingdom),
            nameof(Quest.OnVillageRaid), nameof(Quest.OnMapEventStarted), nameof(Quest.OnMapEventEnded), nameof(Quest.OnSettlementOwnerChanged),
            nameof(Quest.OnPlayerDeserted) })
            yield return AccessTools.Method(typeof(Quest), name);
    }

    private static bool Prefix(QuestBase __instance, MethodBase __originalMethod, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest quest) return true;
        if (ModInformation.IsClient)
            return false;
        if (!ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return true;
        if (execution.Current?.Session.Quest == quest) return true;
        if (!ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) || sessions.Get(quest) is not { } session) return false;

        if (__originalMethod.Name == "HourlyTick" && !quest.CollectingRevenues &&
            (quest._allRevenuesAreCollected || !quest.RevenueVillages.All(village => village.GetIsCompleted()))) return false;

        __state = execution.Enter(session);
        if (__originalMethod.Name == "HourlyTick" && quest.CollectingRevenues)
        {
            var party = session.PlayerHero.PartyBelongedTo;
            if (!session.PlayerHero.IsAlive || session.PlayerHero.IsPrisoner || party == null ||
                party.CurrentSettlement != session.CollectionSettlement || party.MapEvent != null ||
                (ContainerProvider.TryResolve<IPlayerManager>(out var players) && players.IsOwnerOfHeroDisconnected(session.PlayerHero)))
                quest.CollectingRevenues = false;
        }
        return true;
    }

    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.FinalizeQuest))]
internal class RevenueFarmingTerminalStatePatch
{
    private static void Prefix(QuestBase __instance)
    {
        if (ModInformation.IsClient || __instance is not Quest quest ||
            !ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution) ||
            execution.Current?.Session is not { } session || session.Quest != quest) return;
        quest.CollectingRevenues = false;
        session.AwaitingPayment = false;
        session.Incident = null;
        // The journal must arrive before framework finalization removes the issue.
        MessageBroker.Instance.Publish(quest, new RevenueFarmingChanged(session, isFinalizing: true));
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.OnBeforeTimedOut))]
internal class RevenueFarmingTimeoutPatch
{
    private static bool Prefix(ref bool doNotResolveTheQuest)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return true;
        var session = execution.Current?.Session;
        if (session?.AwaitingPayment != true) return true;
        doNotResolveTheQuest = session.PlayerHero.Gold >= session.Quest._totalRequestedDenars;
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
internal class RevenueFarmingTimeoutLogPatch
{
    private static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.ShowQuestResolvePopUp))]
internal class RevenueFarmingPaymentPromptPatch
{
    private static bool Prefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return true;
        var session = execution.Current?.Session;
        if (session?.Quest != __instance) return false;
        if (ModInformation.IsClient) return ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) && sessions.IsLocal(__instance);
        session.AwaitingPayment = true;
        __instance.CollectingRevenues = false;
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
internal class RevenueFarmingAcceptedPatch
{
    private static bool Prefix(Quest __instance)
    {
        if (ModInformation.IsServer || !__instance.IsOngoing ||
            __instance.QuestGiver.Issue?.IssueQuest != __instance ||
            Campaign.Current.QuestManager.Quests.Contains(__instance)) return false;
        if (__instance._questProgressLog == null) return true;
        // A raid update can arrive before the player finishes the acceptance dialogue.
        __instance.StartQuest();
        foreach (var village in __instance.RevenueVillages.Where(village => !village.GetIsCompleted()))
            __instance.AddTrackedObject(village.Village.Settlement);
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class RevenueFarmingPaymentActionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.QuestCompletedWithSuccess));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.QuestCompletedWithBetray));
    }

    private static bool Prefix(Quest __instance, MethodBase __originalMethod)
    {
        if (ModInformation.IsServer) return !ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution) ||
            execution.Current?.Session.Quest == __instance;
        if (!ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) ||
            !ContainerProvider.TryResolve<IRevenueFarmingActions>(out var actions)) return false;
        var demand = sessions.Get(__instance)?.AwaitingPayment == true;
        var pay = __originalMethod.Name == "QuestCompletedWithSuccess";
        actions.Request(__instance, pay
            ? (demand ? RevenueFarmingAction.PayDemand : RevenueFarmingAction.PayGiver)
            : (demand ? RevenueFarmingAction.BetrayDemand : RevenueFarmingAction.BetrayGiver));
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class RevenueFarmingMenuActionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.collect_revenue_menu_consequence));
        yield return AccessTools.Method(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.leave_consequence));
        yield return AccessTools.Method(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.talk_to_steward_on_consequence));
    }

    private static bool Prefix(MethodBase __originalMethod)
    {
        if (ModInformation.IsServer) return !ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution) ||
            execution.Current != null;
        if (ContainerProvider.TryResolve<IRevenueFarmingActions>(out var actions))
        {
            actions.Request(RevenueFarmingIssueBehavior.Instance, __originalMethod.Name switch
            {
                "leave_consequence" => RevenueFarmingAction.Leave,
                "talk_to_steward_on_consequence" => RevenueFarmingAction.PaySteward,
                _ => RevenueFarmingAction.Collect,
            });
        }
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.AddVillageEventMenus))]
internal class RevenueFarmingIncidentOptionsPatch
{
    private static void Prefix(RevenueFarmingIssueBehavior.VillageEvent villageEvent)
    {
        if (ModInformation.IsServer) return;
        for (var i = 0; i < villageEvent.OptionConditionsAndConsequences.Count; i++)
        {
            var index = i;
            var option = villageEvent.OptionConditionsAndConsequences[i];
            villageEvent.OptionConditionsAndConsequences[i] = new RevenueFarmingIssueBehavior.VillageEventOptionData(
                option.Text, args => RevenueFarmingIssueBehavior.Instance != null && option.OnCondition(args), args =>
                {
                    if (ContainerProvider.TryResolve<IRevenueFarmingActions>(out var actions))
                        actions.Request(RevenueFarmingIssueBehavior.Instance, RevenueFarmingAction.ResolveIncident, villageEvent.Id, index);
                }, option.IsLeave);
        }
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.collecting_menu_on_init))]
internal class RevenueFarmingCollectionMenuPatch
{
    private static bool Prefix(MenuCallbackArgs args)
    {
        var quest = RevenueFarmingIssueBehavior.Instance;
        if (ModInformation.IsClient && quest != null && Settlement.CurrentSettlement?.Village != null)
        {
            args.MenuContext.GameMenu.StartWait();
            RevenueFarmingIssueBehavior.UpdateCollectionMenuProgress(args);
        }
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(GameMenu), nameof(GameMenu.SwitchToMenu))]
internal class RevenueFarmingMenuCapturePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(string menuId)
    {
        if (!ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return true;
        var scope = execution.Current;
        if (ModInformation.IsClient || scope == null) return true;
        scope.Menu = menuId;
        var session = scope.Session;
        session.Quest.CollectingRevenues = menuId == "village_collect_revenue";
        if (session.Quest._currentVillageEvents.ContainsKey(menuId)) session.Incident = menuId;
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish))]
internal class RevenueFarmingEncounterCapturePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix()
    {
        if (!ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return true;
        var scope = execution.Current;
        if (ModInformation.IsClient || scope == null) return true;
        scope.FinishEncounter = true;
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(MapEventHelper), nameof(MapEventHelper.OnConversationEnd))]
internal class RevenueFarmingConversationEndPatch
{
    private static bool Prefix()
    {
        if (!ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return true;
        var session = execution.Current?.Session;
        return session == null || (ModInformation.IsClient &&
            ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) && sessions.IsLocal(session.Quest));
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Campaign), nameof(Campaign.TimeControlMode), MethodType.Setter)]
internal class RevenueFarmingTimeControlPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix() => ModInformation.IsClient ||
        !ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution) || execution.Current == null;
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Settlement), nameof(Settlement.CurrentSettlement), MethodType.Getter)]
internal class RevenueFarmingSettlementPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ref Settlement __result)
    {
        if (!ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return true;
        var session = execution.Current?.Session;
        if (ModInformation.IsClient || session == null) return true;
        __result = session.PlayerHero.PartyBelongedTo?.CurrentSettlement;
        return false;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess))]
internal class RevenueFarmingSuccessGuardPatch
{
    [HarmonyPriority(Priority.VeryHigh)]
    private static bool Prefix(QuestBase __instance)
    {
        if (__instance is not Quest) return true;
        if (ModInformation.IsClient) return QuestEndingReplay.IsActive;
        // Generic success skips the vanilla branch that pays the giver or steward.
        if (!ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return true;
        return execution.Current?.Session.Quest == __instance &&
            execution.Current.Action is RevenueFarmingAction.PayGiver or
                RevenueFarmingAction.PayDemand or RevenueFarmingAction.PaySteward;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class RevenueFarmingFinalizationScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { nameof(QuestBase.CompleteQuestWithSuccess), nameof(QuestBase.CompleteQuestWithFail),
            nameof(QuestBase.CompleteQuestWithCancel), nameof(QuestBase.CompleteQuestWithBetrayal), nameof(QuestBase.CompleteQuestWithTimeOut) })
            yield return AccessTools.Method(typeof(QuestBase), name);
    }

    [HarmonyPriority(Priority.First)]
    private static void Prefix(QuestBase __instance, out IDisposable __state)
    {
        __state = null;
        if (ModInformation.IsClient || __instance is not Quest quest ||
            !ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution) || execution.Current?.Session.Quest == quest) return;
        if (ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) && sessions.Get(quest) is { } session)
            __state = execution.Enter(session);
    }

    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
