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
    void Forget(IssueBase issue);
}

internal class MerchantOutlawsProgress : IMerchantOutlawsProgress
{
    private readonly Dictionary<IssueBase, (JournalLog[] Entries, int Progress, int Range)> journals =
        new Dictionary<IssueBase, (JournalLog[], int, int)>();
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

        quest.JournalEntries.FirstOrDefault(log => log.Range == quest._totalPartyCount)
            ?.UpdateCurrentProgress(quest._questPartyProgress);

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

        var tracked = new List<string>();
        if (!final)
        {
            foreach (var party in Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest).OfType<TaleWorlds.CampaignSystem.Party.MobileParty>())
                if (objectManager.TryGetIdWithLogging(party, out var id)) tracked.Add(id);
        }

        network.Send(peer, new NetworkMerchantOutlawsProgress(giverId, issue.StringId, controllerId,
            quest._destroyedPartyCount, quest._recruitedPartyCount, ids.ToArray(), tracked.ToArray()));
    }

    public void Send(IssueBase issue)
    {
        if (ModInformation.IsClient || !issue.IsSolvingWithAlternative || issue.JournalEntries.Count == 0 ||
            !ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !players.TryGetPeer(controllerId, out var peer) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;

        var entries = issue.JournalEntries;
        var current = entries[0].CurrentProgress;
        var range = entries[0].Range;
        if (journals.TryGetValue(issue, out var previous) && previous.Progress == current && previous.Range == range &&
            previous.Entries.Length == entries.Count && previous.Entries.SequenceEqual(entries)) return;

        network.Send(peer, new NetworkMerchantOutlawsProgress(giverId, issue.StringId, controllerId,
            0, 0, Array.Empty<string>(), Array.Empty<string>(),
            issue.JournalEntries.Select(log => log.LogText?.ToString() ?? string.Empty).ToArray(),
            current, range));
        journals[issue] = (entries.ToArray(), current, range);
    }

    public void Forget(IssueBase issue) => journals.Remove(issue);
}
