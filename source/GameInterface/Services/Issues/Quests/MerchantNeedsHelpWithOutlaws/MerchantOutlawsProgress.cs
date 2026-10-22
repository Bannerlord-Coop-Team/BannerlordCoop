using Common;
using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
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
    private readonly IPlayerManager players;

    public MerchantOutlawsProgress(IObjectManager objectManager, IIssueOwnerResolver ownerResolver, INetwork network,
        IPlayerManager players)
    {
        this.objectManager = objectManager;
        this.ownerResolver = ownerResolver;
        this.network = network;
        this.players = players;
    }

    public void Send(Quest quest, bool final = false)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !players.TryGetPeer(controllerId, out var peer) ||
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

        network.Send(peer, new NetworkMerchantOutlawsProgress(giverId, issue.StringId, controllerId,
            quest._destroyedPartyCount, quest._recruitedPartyCount, ids.ToArray(),
            final ? quest.JournalEntries.LastOrDefault()?.LogText?.ToString() ?? string.Empty : string.Empty));
    }

    public void Send(IssueBase issue)
    {
        if (ModInformation.IsClient || !issue.IsSolvingWithAlternative || issue.JournalEntries.Count == 0 ||
            !ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !players.TryGetPeer(controllerId, out var peer) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;

        network.Send(peer, new NetworkMerchantOutlawsProgress(giverId, issue.StringId, controllerId,
            0, 0, Array.Empty<string>(), string.Empty,
            issue.JournalEntries.Select(log => log.LogText?.ToString() ?? string.Empty).ToArray(),
            issue.JournalEntries[0].CurrentProgress, issue.JournalEntries[0].Range));
    }
}
