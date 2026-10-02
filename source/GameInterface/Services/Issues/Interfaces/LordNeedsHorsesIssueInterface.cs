using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

public interface ILordNeedsHorsesIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(
        LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue issue,
        out ItemObject mountObjectToBeDelivered,
        out int numMountsToBeDelivered,
        out int mountValuePerUnit);

    LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue ConstructReplicated(
        Hero owner, ItemObject mountObjectToBeDelivered, int numMountsToBeDelivered, int mountValuePerUnit);

    void RegisterReplicated(Hero owner, LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue issue);
}

public class LordNeedsHorsesIssueInterface : ILordNeedsHorsesIssueInterface
{
    // These two vanilla fields are readonly even after publicizing.
    private static readonly FieldInfo MountObjectField =
        AccessTools.Field(typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue), "_mountObjectToBeDelivered");
    private static readonly FieldInfo MountValuePerUnitField =
        AccessTools.Field(typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue), "_mountValuePerUnit");

    public bool TryCaptureFields(
        LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue issue,
        out ItemObject mountObjectToBeDelivered,
        out int numMountsToBeDelivered,
        out int mountValuePerUnit)
    {
        mountObjectToBeDelivered = null;
        numMountsToBeDelivered = 0;
        mountValuePerUnit = 0;
        if (issue == null) return false;

        mountObjectToBeDelivered = issue._mountObjectToBeDelivered;
        numMountsToBeDelivered = issue._numMountsToBeDelivered;
        mountValuePerUnit = issue._mountValuePerUnit;
        return mountObjectToBeDelivered != null;
    }

    public LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue ConstructReplicated(
        Hero owner, ItemObject mountObjectToBeDelivered, int numMountsToBeDelivered, int mountValuePerUnit)
    {
        var issue = new LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue(owner);

        MountObjectField.SetValue(issue, mountObjectToBeDelivered);
        issue._numMountsToBeDelivered = numMountsToBeDelivered;
        MountValuePerUnitField.SetValue(issue, mountValuePerUnit);

        return issue;
    }

    public void RegisterReplicated(Hero owner, LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
