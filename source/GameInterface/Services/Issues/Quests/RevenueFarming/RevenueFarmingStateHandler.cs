using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Messages;
using LiteNetLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

using Quest = RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

internal sealed class RevenueFarmingStateHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IRevenueFarmingSessionStore sessions;
    private readonly IRevenueFarmingActions actions;

    public RevenueFarmingStateHandler(IMessageBroker broker, INetwork network, IObjectManager objects, IPlayerManager players,
        IIssueOwnershipRegistry ownership, IRevenueFarmingSessionStore sessions, IRevenueFarmingActions actions)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.players = players;
        this.ownership = ownership;
        this.sessions = sessions;
        this.actions = actions;
        broker.Subscribe<RequestRevenueFarmingAction>(HandleRequest);
        broker.Subscribe<RevenueFarmingChanged>(HandleChanged);
        broker.Subscribe<NetworkRevenueFarmingState>(HandleState);
        broker.Subscribe<SwitchedPlayer>(HandleSwitchedPlayer);
        broker.Subscribe<ChangePlayerCharacterAfterHeirSelection>(HandleHeirSelection);
    }

    public void Dispose()
    {
        broker.Unsubscribe<RequestRevenueFarmingAction>(HandleRequest);
        broker.Unsubscribe<RevenueFarmingChanged>(HandleChanged);
        broker.Unsubscribe<NetworkRevenueFarmingState>(HandleState);
        broker.Unsubscribe<SwitchedPlayer>(HandleSwitchedPlayer);
        broker.Unsubscribe<ChangePlayerCharacterAfterHeirSelection>(HandleHeirSelection);
    }

    private void HandleHeirSelection(MessagePayload<ChangePlayerCharacterAfterHeirSelection> payload)
    {
        if (ModInformation.IsClient) return;
        // QuestManager's player-change callback is client-only in co-op; cancel only the old owner's agreement.
        sessions.FindOngoing(payload.What.OriginalHero)?.CompleteQuestWithCancel(new TextObject(
            "{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
    }

    private void HandleRequest(MessagePayload<RequestRevenueFarmingAction> payload)
    {
        if (ModInformation.IsClient || payload.Who is not NetPeer peer) return;
        GameThread.RunSafe(() =>
        {
            var request = payload.What;
            if (request.Action != RevenueFarmingAction.Collect && request.Action != RevenueFarmingAction.Leave &&
                request.Action != RevenueFarmingAction.ResolveIncident && request.Action != RevenueFarmingAction.Refresh) return;
            if (!players.TryGetPlayer(peer, out var player) ||
                !objects.TryGetObjectWithLogging<Hero>(request.GiverId, out var giver) ||
                giver.Issue?.IssueQuest is not Quest quest || quest.StringId != request.QuestId ||
                !ownership.TryGetOwner(request.GiverId, giver.Issue.StringId, out var controller) || controller != player.ControllerId ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return;
            var session = sessions.Get(quest);
            if (session?.PlayerHero != hero) return;
            if ((request.Action == RevenueFarmingAction.Refresh || !actions.Execute(session, request)) && quest.IsOngoing)
            {
                var state = Capture(session);
                state.RefreshPresentation = true;
                network.Send(peer, state);
            }
        });
    }

    private void HandleSwitchedPlayer(MessagePayload<SwitchedPlayer> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>())
            {
                Campaign.Current.ConversationManager.RemoveRelatedLines(quest);
                if (!sessions.IsLocal(quest))
                {
                    Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(quest);
                    continue;
                }
                quest.AddDialogs();
                if (!quest.IsTracked(quest.QuestGiver)) quest.AddTrackedObject(quest.QuestGiver);
                foreach (var village in quest.RevenueVillages.Where(village => !village.GetIsCompleted()))
                    if (!quest.IsTracked(village.Village.Settlement)) quest.AddTrackedObject(village.Village.Settlement);
                actions.Request(quest, RevenueFarmingAction.Refresh);
            }
        });
    }

    private void HandleChanged(MessagePayload<RevenueFarmingChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var changed = payload.What;
        var state = Capture(changed.Session);
        state.Revision = ++changed.Session.Revision;
        state.Menu = changed.Menu;
        state.FinishEncounter = changed.FinishEncounter;
        state.IsFinalizing = changed.IsFinalizing;
        state.RefreshPresentation = changed.RefreshPresentation;
        if (changed.Menu != null && changed.Session.PlayerHero.PartyBelongedTo?.CurrentSettlement is { } settlement)
        {
            if (!objects.TryGetIdWithLogging(settlement, out var settlementId)) return;
            state.MenuSettlementId = settlementId;
        }
        network.SendAll(state);
    }

    private NetworkRevenueFarmingState Capture(RevenueFarmingSession session)
    {
        var quest = session.Quest;
        if (!objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId))
            throw new InvalidOperationException("Revenue Farming giver is not registered.");
        string collectionId = null;
        if (session.CollectionSettlement != null && !objects.TryGetIdWithLogging(session.CollectionSettlement, out collectionId))
            throw new InvalidOperationException("Revenue Farming collection settlement is not registered.");
        return new NetworkRevenueFarmingState
        {
            GiverId = giverId,
            QuestId = quest.StringId,
            Revision = session.Revision,
            RequestedDenars = quest._totalRequestedDenars,
            Collecting = quest.CollectingRevenues,
            AllCollected = quest._allRevenuesAreCollected,
            Incident = session.Incident,
            AwaitingPayment = session.AwaitingPayment,
            CollectionSettlementId = collectionId,
            Menu = null,
            Villages = quest.RevenueVillages.Select(village =>
            {
                if (!objects.TryGetIdWithLogging(village.Village.Settlement, out var id))
                    throw new InvalidOperationException("Revenue Farming village is not registered.");
                return new RevenueFarmingVillageState
                {
                    SettlementId = id, CollectedAmount = village.CollectedAmount, HourlyGain = village.HourlyGain,
                    Done = village._isDone, EventOccurred = village.EventOccurred, Raided = village.IsRaided,
                    AdditionalProgress = village._customProgress,
                };
            }).ToArray(),
            UsedIncidents = quest._currentVillageEvents.Where(pair => pair.Value).Select(pair => pair.Key).ToArray(),
            Journal = quest.JournalEntries.Select(log => new RevenueFarmingJournalState
            {
                TimeTicks = log.LogTime.NumTicks, Text = log.LogText, TaskName = log.TaskName,
                Progress = log.CurrentProgress, Range = log.Range, Type = (int)log.Type,
            }).ToArray(),
            ProgressLogIndex = quest._journalEntries.IndexOf(quest._questProgressLog),
        };
    }

    private void HandleState(MessagePayload<NetworkRevenueFarmingState> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            var state = payload.What;
            if (!objects.TryGetObjectWithLogging<Hero>(state.GiverId, out var giver)) return;
            // Steward navigation arrives after framework completion removes the giver's issue.
            var session = sessions.Get(giver, state.QuestId);
            if (session == null || state.Revision < session.Revision) return;
            var quest = session.Quest;
            Validate(state, quest);
            Settlement collection = null;
            if (state.CollectionSettlementId != null &&
                !objects.TryGetObjectWithLogging(state.CollectionSettlementId, out collection)) return;
            Settlement menuSettlement = null;
            if (state.MenuSettlementId != null &&
                !objects.TryGetObjectWithLogging(state.MenuSettlementId, out menuSettlement)) return;
            var villages = state.Villages.Select(value =>
            {
                if (!objects.TryGetObjectWithLogging<Settlement>(value.SettlementId, out var settlement))
                    throw new InvalidOperationException("Revenue Farming state references a missing village.");
                return quest.RevenueVillages.Single(village => village.Village.Settlement == settlement);
            }).ToArray();
            var newLog = state.Journal.Length > quest.JournalEntries.Count;
            var newPrompt = state.AwaitingPayment && (!session.AwaitingPayment || state.RefreshPresentation);
            using (new AllowedThread())
            {
                session.Revision = state.Revision;
                session.Incident = state.Incident;
                session.AwaitingPayment = state.AwaitingPayment;
                session.CollectionSettlement = collection;
                quest._totalRequestedDenars = state.RequestedDenars;
                quest.CollectingRevenues = state.Collecting;
                quest._allRevenuesAreCollected = state.AllCollected;
                for (var i = 0; i < villages.Length; i++)
                {
                    var value = state.Villages[i];
                    var village = villages[i];
                    village.CollectedAmount = value.CollectedAmount;
                    village.HourlyGain = value.HourlyGain;
                    village._isDone = value.Done;
                    village.EventOccurred = value.EventOccurred;
                    village.IsRaided = value.Raided;
                    village._customProgress = value.AdditionalProgress;
                    if (village.GetIsCompleted()) quest.RemoveTrackedObject(village.Village.Settlement);
                }
                foreach (var key in quest._currentVillageEvents.Keys.ToArray())
                    quest._currentVillageEvents[key] = state.UsedIncidents.Contains(key);
                quest._journalEntries.Clear();
                foreach (var log in state.Journal)
                    quest._journalEntries.Add(new JournalLog(new CampaignTime(log.TimeTicks), log.Text, log.TaskName,
                        log.Progress, log.Range, (LogType)log.Type));
                quest._questProgressLog = quest._journalEntries[state.ProgressLogIndex];
            }
            if (!sessions.IsLocal(quest)) return;
            if (newLog) CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, false);
            // Vanilla finalization needs the current revenue menu to decide whether to finish the encounter.
            if (!state.IsFinalizing) Present(session, state, newPrompt, menuSettlement);
        });
    }

    private static void Validate(NetworkRevenueFarmingState state, Quest quest)
    {
        if (state.Villages == null || state.Villages.Length != quest.RevenueVillages.Count ||
            state.Villages.Any(village => village == null || village.CollectedAmount < 0 || village.HourlyGain < 0 ||
                float.IsNaN(village.AdditionalProgress) || float.IsInfinity(village.AdditionalProgress)) ||
            state.Villages.Select(village => village.SettlementId).Distinct().Count() != state.Villages.Length ||
            state.Journal == null || state.ProgressLogIndex < 0 || state.ProgressLogIndex >= state.Journal.Length ||
            state.Journal.Any(log => log?.Text == null) || state.UsedIncidents == null ||
            state.UsedIncidents.Any(id => !quest._currentVillageEvents.ContainsKey(id)) ||
            (state.Incident != null && !quest._currentVillageEvents.ContainsKey(state.Incident)))
            throw new ArgumentException("Revenue Farming state does not match the accepted quest.");
    }

    private static void Present(RevenueFarmingSession session, NetworkRevenueFarmingState state, bool newPrompt, Settlement menuSettlement)
    {
        if (newPrompt)
        {
            using (new RevenueFarmingExecutionScope(session, publish: false)) session.Quest.ShowQuestResolvePopUp();
        }
        var currentMenu = Campaign.Current.CurrentMenuContext?.GameMenu?.StringId;
        if (state.Menu == "town" || state.Menu == "castle")
        {
            if (menuSettlement != null && Settlement.CurrentSettlement == menuSettlement &&
                (currentMenu == "town_guard" || currentMenu == "castle_guard" || currentMenu == "town" || currentMenu == "castle"))
                GameMenu.SwitchToMenu(state.Menu);
            return;
        }
        if (Settlement.CurrentSettlement == null || Settlement.CurrentSettlement != session.CollectionSettlement) return;
        var isRevenueMenu = currentMenu == "village_collect_revenue" ||
            (currentMenu != null && session.Quest._currentVillageEvents.ContainsKey(currentMenu));
        if (state.FinishEncounter && isRevenueMenu)
        {
            if (state.Menu != null) GameMenu.SwitchToMenu(state.Menu);
            PlayerEncounter.Finish();
        }
        else
        {
            var menu = state.Menu ?? (state.RefreshPresentation ? session.Incident : null);
            if (menu == null && isRevenueMenu && !state.Collecting && state.Incident == null) menu = "village";
            if (menu != null && (isRevenueMenu || currentMenu == "village")) GameMenu.SwitchToMenu(menu);
        }
    }
}
