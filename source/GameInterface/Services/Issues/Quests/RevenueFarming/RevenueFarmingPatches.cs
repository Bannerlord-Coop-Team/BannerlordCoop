using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
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

[HarmonyPatch(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.OnCheckForIssue))]
internal class RevenueFarmingOfferAuthorityPatch
{
    private static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.Instance), MethodType.Getter)]
internal class RevenueFarmingInstancePatch
{
    private static bool Prefix(ref Quest __result)
    {
        __result = RevenueFarmingExecutionScope.Current?.Session.Quest;
        if (__result == null && ModInformation.IsClient && ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions))
            __result = sessions.LocalQuest;
        return false;
    }
}

[HarmonyPatch(typeof(RevenueFarmingIssueBehavior), nameof(RevenueFarmingIssueBehavior.SyncData))]
internal class RevenueFarmingSavePatch
{
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions)) sessions.SyncData(dataStore);
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnIssueUpdated))]
internal class RevenueFarmingIssueEffectsPatch
{
    private static bool Prefix(IssueBase issue) => issue is not RevenueFarmingIssueBehavior.RevenueFarmingIssue || ModInformation.IsServer;
}

[HarmonyPatch]
internal class RevenueFarmingCallbackPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { nameof(Quest.HourlyTick), nameof(Quest.OnWarDeclared), nameof(Quest.OnClanChangedKingdom),
            nameof(Quest.OnVillageRaid), nameof(Quest.OnMapEventStarted), nameof(Quest.OnMapEventEnded), nameof(Quest.OnSettlementOwnerChanged) })
            yield return AccessTools.Method(typeof(Quest), name);
    }

    private static bool Prefix(QuestBase __instance, MethodBase __originalMethod, out RevenueFarmingExecutionScope __state)
    {
        __state = null;
        if (__instance is not Quest quest) return true;
        if (ModInformation.IsClient)
            return false;
        if (RevenueFarmingExecutionScope.Current?.Session.Quest == quest) return true;
        if (!ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) || sessions.Get(quest) is not { } session) return false;

        if (__originalMethod.Name == "HourlyTick" && !quest.CollectingRevenues &&
            (quest._allRevenuesAreCollected || !quest.RevenueVillages.All(village => village.GetIsCompleted()))) return false;

        __state = new RevenueFarmingExecutionScope(session);
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

    private static void Finalizer(RevenueFarmingExecutionScope __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.FinalizeQuest))]
internal class RevenueFarmingTerminalStatePatch
{
    private static void Prefix(QuestBase __instance)
    {
        if (ModInformation.IsClient || __instance is not Quest quest ||
            RevenueFarmingExecutionScope.Current?.Session is not { } session || session.Quest != quest) return;
        quest.CollectingRevenues = false;
        session.AwaitingPayment = false;
        session.Incident = null;
        // The journal must arrive before framework finalization removes the issue.
        MessageBroker.Instance.Publish(quest, new RevenueFarmingChanged(session, isFinalizing: true));
    }
}

[HarmonyPatch(typeof(Quest), nameof(Quest.OnBeforeTimedOut))]
internal class RevenueFarmingTimeoutPatch
{
    private static bool Prefix(ref bool doNotResolveTheQuest)
    {
        if (ModInformation.IsClient) return false;
        var session = RevenueFarmingExecutionScope.Current?.Session;
        if (session?.AwaitingPayment != true) return true;
        doNotResolveTheQuest = session.PlayerHero.Gold >= session.Quest._totalRequestedDenars;
        return false;
    }
}

[HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
internal class RevenueFarmingTimeoutLogPatch
{
    private static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(Quest), nameof(Quest.ShowQuestResolvePopUp))]
internal class RevenueFarmingPaymentPromptPatch
{
    private static bool Prefix(Quest __instance)
    {
        var session = RevenueFarmingExecutionScope.Current?.Session;
        if (session?.Quest != __instance) return false;
        if (ModInformation.IsClient) return ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) && sessions.IsLocal(__instance);
        session.AwaitingPayment = true;
        __instance.CollectingRevenues = false;
        return false;
    }
}

[HarmonyPatch(typeof(Quest), nameof(Quest.OnPlayerDeserted))]
internal class RevenueFarmingDesertionPatch
{
    private static bool Prefix(Quest __instance)
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IRevenueFarmingActions>(out var actions))
            actions.Request(__instance, RevenueFarmingAction.Leave);
        return ModInformation.IsServer && RevenueFarmingExecutionScope.Current?.Session.Quest == __instance;
    }
}

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
        if (ModInformation.IsServer) return RevenueFarmingExecutionScope.Current?.Session.Quest == __instance;
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
        if (ModInformation.IsServer) return RevenueFarmingExecutionScope.Current != null;
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

[HarmonyPatch(typeof(GameMenu), nameof(GameMenu.SwitchToMenu))]
internal class RevenueFarmingMenuCapturePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(string menuId)
    {
        var scope = RevenueFarmingExecutionScope.Current;
        if (ModInformation.IsClient || scope == null) return true;
        scope.Menu = menuId;
        var session = scope.Session;
        session.Quest.CollectingRevenues = menuId == "village_collect_revenue";
        if (session.Quest._currentVillageEvents.ContainsKey(menuId)) session.Incident = menuId;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish))]
internal class RevenueFarmingEncounterCapturePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix()
    {
        var scope = RevenueFarmingExecutionScope.Current;
        if (ModInformation.IsClient || scope == null) return true;
        scope.FinishEncounter = true;
        return false;
    }
}

[HarmonyPatch(typeof(MapEventHelper), nameof(MapEventHelper.OnConversationEnd))]
internal class RevenueFarmingConversationEndPatch
{
    private static bool Prefix()
    {
        var session = RevenueFarmingExecutionScope.Current?.Session;
        return session == null || (ModInformation.IsClient &&
            ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) && sessions.IsLocal(session.Quest));
    }
}

[HarmonyPatch(typeof(Campaign), nameof(Campaign.TimeControlMode), MethodType.Setter)]
internal class RevenueFarmingTimeControlPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix() => ModInformation.IsClient || RevenueFarmingExecutionScope.Current == null;
}

[HarmonyPatch(typeof(Settlement), nameof(Settlement.CurrentSettlement), MethodType.Getter)]
internal class RevenueFarmingSettlementPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ref Settlement __result)
    {
        var session = RevenueFarmingExecutionScope.Current?.Session;
        if (ModInformation.IsClient || session == null) return true;
        __result = session.PlayerHero.PartyBelongedTo?.CurrentSettlement;
        return false;
    }
}

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
    private static void Prefix(QuestBase __instance, out RevenueFarmingExecutionScope __state)
    {
        __state = null;
        if (ModInformation.IsClient || __instance is not Quest quest || RevenueFarmingExecutionScope.Current?.Session.Quest == quest) return;
        if (ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) && sessions.Get(quest) is { } session)
            __state = new RevenueFarmingExecutionScope(session);
    }

    private static void Finalizer(RevenueFarmingExecutionScope __state) => __state?.Dispose();
}
