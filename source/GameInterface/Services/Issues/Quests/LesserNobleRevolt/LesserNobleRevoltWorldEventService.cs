using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal interface ILesserNobleRevoltWorldEventService
{
    void SendOwnerBattleEnded(Quest quest);
    void SendEndingLog(IssueBase issue, Quest quest);
}

internal sealed class LesserNobleRevoltWorldEventService : ILesserNobleRevoltWorldEventService
{
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public LesserNobleRevoltWorldEventService(
        IIssueOwnerResolver ownerResolver,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.ownerResolver = ownerResolver;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
    }

    public void SendOwnerBattleEnded(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null ||
            !ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId))
        {
            return;
        }

        network.SendAll(new NetworkLesserNobleRevoltOwnerBattleEnded(issueOwnerId, issue.StringId, controllerId));
    }

    public void SendEndingLog(IssueBase issue, Quest quest)
    {
        // A dismissed quest has no owner left to tell.
        if (!ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !playerManager.TryGetPeer(controllerId, out var peer) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId))
        {
            return;
        }

        var logs = quest.JournalEntries.Select(log => log.LogText).ToArray();
        network.Send(peer, new NetworkLesserNobleRevoltEndingLog(issueOwnerId, issue.StringId, controllerId, logs));
    }
}
