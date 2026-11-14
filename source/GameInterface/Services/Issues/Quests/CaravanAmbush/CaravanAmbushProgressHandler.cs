using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using Common.Util;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.Core;
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
    private readonly ICaravanAmbushRewardConversation rewardConversation;

    public CaravanAmbushProgressHandler(IMessageBroker broker, IObjectManager objects, IIssueResolver issues,
        IControllerIdProvider controller, IIssueConversationGuard conversationGuard,
        ICaravanAmbushRewardConversation rewardConversation)
    {
        this.broker = broker;
        this.objects = objects;
        this.issues = issues;
        this.controller = controller;
        this.conversationGuard = conversationGuard;
        this.rewardConversation = rewardConversation;
        broker.Subscribe<NetworkCaravanAmbushProgress>(Handle);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkCaravanAmbushProgress>(Handle);
    }

    private void Handle(MessagePayload<NetworkCaravanAmbushProgress> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controller.ControllerId) return;
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() => Apply(data), data.IssueOwnerId),
            context: nameof(CaravanAmbushProgressHandler));
    }

    private void Apply(NetworkCaravanAmbushProgress data)
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
        if (data.OpenRewardConversation) rewardConversation.Request(data.IssueOwnerId, data.IssueId);
    }

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
        // Keep the last reference for a removed party so vanilla reward dialogue remains available.
        if (string.IsNullOrEmpty(id))
        {
            party = previous;
            return true;
        }
        party = null;
        return objects.TryGetObjectWithLogging(id, out party);
    }
}
