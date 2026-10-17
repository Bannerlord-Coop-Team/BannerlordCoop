using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

internal interface IMerchantOutlawsProgress
{
    void Send(Quest quest, bool final = false);
    void Send(IssueBase issue);
}

internal class MerchantOutlawsProgress : IMerchantOutlawsProgress
{
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly INetwork network;

    public MerchantOutlawsProgress(IObjectManager objectManager, IIssueOwnerResolver ownerResolver, INetwork network)
    {
        this.objectManager = objectManager;
        this.ownerResolver = ownerResolver;
        this.network = network;
    }

    public void Send(Quest quest, bool final = false)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId))
        {
            return;
        }

        var ids = new List<string>();
        if (!final)
        {
            foreach (var party in quest._validPartiesList.Distinct())
            {
                if (objectManager.TryGetIdWithLogging(party, out var id)) ids.Add(id);
            }
        }

        network.SendAll(new NetworkMerchantOutlawsProgress(giverId, issue.StringId, controllerId,
            quest._destroyedPartyCount, quest._recruitedPartyCount, ids.ToArray(),
            final ? quest.JournalEntries.LastOrDefault()?.LogText?.ToString() ?? string.Empty : string.Empty));
    }

    public void Send(IssueBase issue)
    {
        if (ModInformation.IsClient || !issue.IsSolvingWithAlternative || issue.JournalEntries.Count == 0 ||
            !ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;

        network.SendAll(new NetworkMerchantOutlawsProgress(giverId, issue.StringId, controllerId,
            0, 0, Array.Empty<string>(), string.Empty,
            issue.JournalEntries.Select(log => log.LogText?.ToString() ?? string.Empty).ToArray(),
            issue.JournalEntries[0].CurrentProgress, issue.JournalEntries[0].Range));
    }
}

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
