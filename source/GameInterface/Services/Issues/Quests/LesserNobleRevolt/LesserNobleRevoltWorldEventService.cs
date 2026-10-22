using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal interface ILesserNobleRevoltWorldEventService
{
    void SendOwnerBattleEnded(Quest quest);
}

internal sealed class LesserNobleRevoltWorldEventService : ILesserNobleRevoltWorldEventService
{
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public LesserNobleRevoltWorldEventService(
        IIssueOwnerResolver ownerResolver,
        IObjectManager objectManager,
        INetwork network)
    {
        this.ownerResolver = ownerResolver;
        this.objectManager = objectManager;
        this.network = network;
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
}
