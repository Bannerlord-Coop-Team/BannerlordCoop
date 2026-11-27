using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Visibility;
using GameInterface.Services.ObjectManager;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

internal class MerchantOutlawsProgressHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IIssueResolver issueResolver;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IMerchantOutlawsProgress progress;

    public MerchantOutlawsProgressHandler(IMessageBroker messageBroker, IIssueResolver issueResolver,
        IObjectManager objectManager, IControllerIdProvider controllerIdProvider, IMerchantOutlawsProgress progress)
    {
        this.messageBroker = messageBroker;
        this.issueResolver = issueResolver;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
        this.progress = progress;
        messageBroker.Subscribe<NetworkMerchantOutlawsProgress>(Handle);
        messageBroker.Subscribe<IssueManagerDailyTicked>(SendAlternativeProgress);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMerchantOutlawsProgress>(Handle);
        messageBroker.Unsubscribe<IssueManagerDailyTicked>(SendAlternativeProgress);
    }

    private void SendAlternativeProgress(MessagePayload<IssueManagerDailyTicked> payload)
    {
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values.OfType<Issue>()) progress.Send(issue);
    }

    private void Handle(MessagePayload<NetworkMerchantOutlawsProgress> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (data.ControllerId != controllerIdProvider.ControllerId ||
                !issueResolver.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _)) return;

            using var replay = new AllowedThread();
            using var journalReplay = new QuestJournalReplay();
            if (data.AlternativeLogs != null && issue is MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue)
            {
                for (var i = issue.JournalEntries.Count; i < data.AlternativeLogs.Length; i++)
                {
                    issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject("{=!}" + data.AlternativeLogs[i]),
                        currentProgress: i == 0 ? data.AlternativeProgress : 0,
                        range: i == 0 ? data.AlternativeRange : 0));
                }

                issue.JournalEntries[0].UpdateCurrentProgress(data.AlternativeProgress);
                return;
            }

            if (!(issue.IssueQuest is Quest quest)) return;

            var hunted = data.Destroyed + data.Recruited;
            // The server's progress step skips vanilla's per-band notice, the last band gets the success journal instead.
            var announce = hunted > quest._questPartyProgress && hunted < quest._totalPartyCount;
            quest._destroyedPartyCount = data.Destroyed;
            quest._recruitedPartyCount = data.Recruited;
            foreach (var party in Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest).OfType<MobileParty>())
                quest.RemoveTrackedObject(party);

            quest._validPartiesList.Clear();
            foreach (var id in data.PartyIds ?? Array.Empty<string>())
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(id, out var party)) continue;
                quest._validPartiesList.Add(party);
            }

            foreach (var id in data.TrackedPartyIds ?? Array.Empty<string>())
            {
                if (objectManager.TryGetObjectWithLogging<MobileParty>(id, out var party) && !quest.IsTracked(party))
                    quest.AddTrackedObject(party);
            }

            quest._questProgressLogTest?.UpdateCurrentProgress(hunted);
            if (announce)
            {
                var notice = new TextObject("{=xbVCRbUu}You hunted {CURRENT_COUNT}/{TOTAL_COUNT} gang of brigands.");
                notice.SetTextVariable("CURRENT_COUNT", hunted);
                notice.SetTextVariable("TOTAL_COUNT", quest._totalPartyCount);
                MBInformationManager.AddQuickInformation(notice);
            }
        }, context: nameof(MerchantOutlawsProgressHandler));
    }
}
