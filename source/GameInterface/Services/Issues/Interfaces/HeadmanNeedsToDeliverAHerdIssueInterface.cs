using Common.Util;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

public interface IHeadmanNeedsToDeliverAHerdIssueInterface
{
    bool TryCaptureFields(Issue issue, out Settlement targetSettlement, out Hero targetHero, out ItemObject herdTypeToDeliver);
    Issue ConstructReplicated(Hero owner, Settlement targetSettlement, Hero targetHero, ItemObject herdTypeToDeliver);
    void RegisterReplicated(Hero owner, Issue issue, string issueId, CampaignTime creationTime, CampaignTime dueTime);
}

public class HeadmanNeedsToDeliverAHerdIssueInterface : IHeadmanNeedsToDeliverAHerdIssueInterface
{
    public bool TryCaptureFields(Issue issue, out Settlement targetSettlement, out Hero targetHero, out ItemObject herdTypeToDeliver)
    {
        targetSettlement = issue?._targetSettlement;
        targetHero = issue?._targetHero;
        herdTypeToDeliver = issue?._herdTypeToDeliver;
        return targetSettlement != null && targetHero != null && herdTypeToDeliver != null;
    }

    public Issue ConstructReplicated(Hero owner, Settlement targetSettlement, Hero targetHero, ItemObject herdTypeToDeliver)
    {
        using (new AllowedThread())
        using (new HeadmanHerdIssueReplicaScope())
        {
            var issue = new Issue(owner);
            issue._targetSettlement = targetSettlement;
            issue._targetHero = targetHero;
            issue._herdTypeToDeliver = herdTypeToDeliver;
            return issue;
        }
    }

    public void RegisterReplicated(Hero owner, Issue issue, string issueId, CampaignTime creationTime, CampaignTime dueTime)
    {
        using (new AllowedThread())
        {
            issue.StringId = issueId;
            issue.IssueCreationTime = creationTime;
            issue.IssueDueTime = dueTime;
            issue.AfterCreation();
            Campaign.Current.IssueManager._issues.Add(owner, issue);
            owner.OnIssueCreatedForHero(issue);
            if (owner.PartyBelongedTo != null)
                issue.AddTrackedObject(owner.PartyBelongedTo);
            CampaignEventDispatcher.Instance.OnNewIssueCreated(issue);
        }
    }
}

internal sealed class HeadmanHerdIssueReplicaScope : IDisposable
{
    [ThreadStatic]
    private static int depth;

    internal static bool IsActive => depth > 0;

    public HeadmanHerdIssueReplicaScope() => depth++;

    public void Dispose() => depth--;
}
