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

    public CaravanAmbushProgressHandler(IMessageBroker broker, IObjectManager objects, IIssueResolver issues,
        IControllerIdProvider controller, IIssueConversationGuard conversationGuard)
    {
        this.broker = broker;
        this.objects = objects;
        this.issues = issues;
        this.controller = controller;
        this.conversationGuard = conversationGuard;
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
        }, data.IssueOwnerId), context: nameof(CaravanAmbushProgressHandler));
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
