using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using Common.Util;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameState;
using Helpers;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushProgressHandler : IHandler
{
    // Vanilla's last proximity warning and the strike that fails the quest, from CaravanAmbushIssueQuest.HourlyTick.
    private const int FinalWarning = 3;
    private const int CaughtAt = 4;

    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;
    private readonly IIssueConversationGuard conversationGuard;
    private (string IssueOwnerId, string IssueId)? pendingConversation;

    public CaravanAmbushProgressHandler(IMessageBroker broker, IObjectManager objects, IIssueResolver issues,
        IControllerIdProvider controller, IIssueConversationGuard conversationGuard)
    {
        this.broker = broker;
        this.objects = objects;
        this.issues = issues;
        this.controller = controller;
        this.conversationGuard = conversationGuard;
        broker.Subscribe<NetworkCaravanAmbushProgress>(Handle);
        broker.Subscribe<CampaignTick>(Tick);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkCaravanAmbushProgress>(Handle);
        broker.Unsubscribe<CampaignTick>(Tick);
        pendingConversation = null;
    }

    private void Handle(MessagePayload<NetworkCaravanAmbushProgress> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controller.ControllerId) return;
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest)) return;
            if (!TryResolveParty(data.CaravanId, quest._caravanParty, out var caravan) ||
                !TryResolveParty(data.BanditId, quest._banditParty, out var bandits)) return;
            quest._caravanParty = caravan;
            quest._banditParty = bandits;
            quest._isCaravanSaved = data.Saved;
            ShowProximityWarning(quest._vicinityCheckFailCounter, data.Warnings);
            quest._vicinityCheckFailCounter = data.Warnings;
            foreach (var party in new[] { caravan, bandits })
            {
                if (party != null && !quest.IsTracked(party)) quest.AddTrackedObject(party);
            }
            ReplayJournal(quest, data.Journal);
            if (data.OpenRewardConversation)
            {
                pendingConversation = (data.IssueOwnerId, data.IssueId);
                TryOpenConversation();
            }
        }, data.IssueOwnerId), context: nameof(CaravanAmbushProgressHandler));
    }

    private void Tick(MessagePayload<CampaignTick> payload)
    {
        // Vanilla TickEvent stops while paused, including after battle loot closes.
        if (ModInformation.IsClient) TryOpenConversation();
    }

    private void TryOpenConversation()
    {
        if (!pendingConversation.HasValue) return;
        var data = pendingConversation.Value;
        if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
            !(issue.IssueQuest is Quest quest) || !quest.IsOngoing)
        {
            pendingConversation = null;
            return;
        }
        if (!quest._isCaravanSaved || quest._caravanParty == null || !quest._caravanParty.IsActive) return;
        if (!IsOwnerFreeOnMap()) return;
        var leader = ConversationHelper.GetConversationCharacterPartyLeader(quest._caravanParty.Party);
        if (leader == null) return;
        pendingConversation = null;
        CampaignMapConversation.OpenConversation(
            new ConversationCharacterData(Hero.MainHero.CharacterObject),
            new ConversationCharacterData(leader));
    }

    private bool IsOwnerFreeOnMap() => GameStateManager.Current?.ActiveState is MapState map && !map.AtMenu &&
        !map.IsSimulationActive && !Campaign.Current.ConversationManager.IsConversationInProgress;

    private static void ShowProximityWarning(int previous, int warnings)
    {
        if (warnings <= previous || warnings >= CaughtAt) return;
        TextObject warning;
        if (warnings == FinalWarning)
            warning = new TextObject("{=uD2pfRAh}Get back immediately! If you keep this close to the caravan the ambushers will certainly notice you.");
        else
            warning = new TextObject("{=ki1CWgcP}Warning! You are too close to the caravan. Stay a bit farther away.");
        MBInformationManager.AddQuickInformation(warning);
    }

    private static void ReplayJournal(Quest quest, IssueJournalEntry[] journal)
    {
        using (new AllowedThread())
        using (new QuestJournalReplay())
        {
            quest._journalEntries.Clear();
            foreach (var entry in journal ?? Array.Empty<IssueJournalEntry>())
                quest._journalEntries.Add(entry.ToJournalLog());
            CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
        }
    }

    private bool TryResolveParty(string id, MobileParty previous, out MobileParty party)
    {
        // An empty id means the party is gone. Keep the last reference so vanilla reward dialogue remains available.
        if (string.IsNullOrEmpty(id))
        {
            party = previous;
            return true;
        }
        party = null;
        return objects.TryGetObjectWithLogging(id, out party);
    }
}
