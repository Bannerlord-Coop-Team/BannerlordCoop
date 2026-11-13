using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using System;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using Helpers;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushStateHandler : IHandler
{
    // Vanilla's last proximity warning and the strike that fails the quest, from CaravanAmbushIssueQuest.HourlyTick.
    private const int FinalWarning = 3;
    private const int CaughtAt = 4;

    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;
    private readonly IIssueConversationGuard conversationGuard;
    private NetworkCaravanAmbushConversation? pendingConversation;

    public CaravanAmbushStateHandler(IMessageBroker broker, IObjectManager objects, IIssueResolver issues,
        IControllerIdProvider controller, IIssueConversationGuard conversationGuard)
    {
        this.broker = broker;
        this.objects = objects;
        this.issues = issues;
        this.controller = controller;
        this.conversationGuard = conversationGuard;
        broker.Subscribe<NetworkCaravanAmbushState>(Handle);
        broker.Subscribe<NetworkCaravanAmbushConversation>(Conversation);
        broker.Subscribe<CampaignTick>(Tick);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkCaravanAmbushState>(Handle);
        broker.Unsubscribe<NetworkCaravanAmbushConversation>(Conversation);
        broker.Unsubscribe<CampaignTick>(Tick);
        pendingConversation = null;
    }

    private void Conversation(MessagePayload<NetworkCaravanAmbushConversation> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controller.ControllerId) return;
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            pendingConversation = data;
            TryOpenConversation();
        }, data.IssueOwnerId), context: nameof(CaravanAmbushStateHandler));
    }

    private void Tick(MessagePayload<CampaignTick> payload)
    {
        // Vanilla TickEvent stops while paused, including after battle loot closes.
        if (ModInformation.IsClient && pendingConversation.HasValue) TryOpenConversation();
    }

    private void TryOpenConversation()
    {
        if (!pendingConversation.HasValue) return;
        var data = pendingConversation.Value;
        // These cases discard the pending conversation.
        if (data.ControllerId != controller.ControllerId ||
            !issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
            !(issue.IssueQuest is Quest quest) || !quest.IsOngoing ||
            !objects.TryGetObjectWithLogging<MobileParty>(data.CaravanId, out var caravan) || !caravan.IsActive)
        {
            pendingConversation = null;
            return;
        }
        // The remaining guards keep it pending until a later tick.
        if (!quest._isCaravanSaved) return;
        if (!IsOwnerFreeOnMap()) return;
        var leader = ConversationHelper.GetConversationCharacterPartyLeader(caravan.Party);
        if (leader == null) return;
        CampaignMapConversation.OpenConversation(
            new ConversationCharacterData(Hero.MainHero.CharacterObject),
            new ConversationCharacterData(leader));
        pendingConversation = null;
    }

    private bool IsOwnerFreeOnMap() => GameStateManager.Current?.ActiveState is MapState map && !map.AtMenu &&
        !map.IsSimulationActive && !Campaign.Current.ConversationManager.IsConversationInProgress;

    private void Handle(MessagePayload<NetworkCaravanAmbushState> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controller.ControllerId) return;
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest)) return;
            if (!TryResolveParty(data.CaravanId, out var caravan) || !TryResolveParty(data.BanditId, out var bandits)) return;
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
        }, data.IssueOwnerId), context: nameof(CaravanAmbushStateHandler));
    }

    private static void ShowProximityWarning(int previous, int warnings)
    {
        if (warnings <= previous || warnings >= CaughtAt) return;
        var warning = warnings == FinalWarning
            ? new TextObject("{=uD2pfRAh}Get back immediately! If you keep this close to the caravan the ambushers will certainly notice you.")
            : new TextObject("{=ki1CWgcP}Warning! You are too close to the caravan. Stay a bit farther away.");
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

    private bool TryResolveParty(string id, out MobileParty party)
    {
        // An empty id means the party is gone and resolves to null.
        party = null;
        return string.IsNullOrEmpty(id) || objects.TryGetObjectWithLogging(id, out party);
    }
}
