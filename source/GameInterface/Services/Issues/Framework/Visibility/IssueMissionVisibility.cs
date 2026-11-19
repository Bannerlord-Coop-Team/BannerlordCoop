using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Visibility;

internal sealed class IssueMissionVisibility : IIssueMissionVisibility
{
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IControllerIdProvider controller;

    public IssueMissionVisibility(IIssueOwnerResolver ownerResolver, IControllerIdProvider controller)
    {
        this.ownerResolver = ownerResolver;
        this.controller = controller;
    }

    public bool IsAnotherPlayersTroopsMission(IssueBase issue)
    {
        if (!issue.IsSolvingWithAlternative && !issue.IsSolvingWithLordSolution) return false;

        return !(ownerResolver.TryGetOwnerControllerId(issue, out var ownerControllerId) &&
            ownerControllerId == controller.ControllerId);
    }
}
