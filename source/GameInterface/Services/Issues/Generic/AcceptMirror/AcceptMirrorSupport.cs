using Common.Util;
using GameInterface.Services.Issues.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

internal static class AcceptMirrorSupport
{
    public static void RejectAcceptance(Hero owner)
    {
        if (owner?.Issue == null || owner.Issue.IsOngoingWithoutQuest) return;
        if (owner.Issue.IssueQuest == null) return;
        if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) && ownershipRegistry.IsLocalPeerOwner(owner)) return;

        using (new AllowedThread())
        {
            owner.Issue.CompleteIssueWithCancel();
        }
    }
}
