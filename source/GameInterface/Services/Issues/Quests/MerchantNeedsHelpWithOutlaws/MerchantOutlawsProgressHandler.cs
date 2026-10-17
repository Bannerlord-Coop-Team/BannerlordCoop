using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

internal class MerchantOutlawsProgressHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IIssueResolver issueResolver;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;

    public MerchantOutlawsProgressHandler(IMessageBroker messageBroker, IIssueResolver issueResolver,
        IObjectManager objectManager, IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.issueResolver = issueResolver;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkMerchantOutlawsProgress>(Handle);
    }

    public void Dispose() => messageBroker.Unsubscribe<NetworkMerchantOutlawsProgress>(Handle);

    private void Handle(MessagePayload<NetworkMerchantOutlawsProgress> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (data.ControllerId != controllerIdProvider.ControllerId ||
                !issueResolver.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _)) return;

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

            quest._destroyedPartyCount = data.Destroyed;
            quest._recruitedPartyCount = data.Recruited;
            foreach (var party in quest._validPartiesList)
            {
                if (quest.IsTracked(party)) quest.RemoveTrackedObject(party);
            }

            quest._validPartiesList.Clear();
            foreach (var id in data.PartyIds ?? Array.Empty<string>())
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(id, out var party)) continue;
                quest._validPartiesList.Add(party);
                if (!quest.IsTracked(party)) quest.AddTrackedObject(party);
            }

            quest._questProgressLogTest?.UpdateCurrentProgress(data.Destroyed + data.Recruited);
            if (!string.IsNullOrEmpty(data.FinalLog)) quest.AddLog(new TextObject("{=!}" + data.FinalLog));
        }, context: nameof(MerchantOutlawsProgressHandler));
    }
}
