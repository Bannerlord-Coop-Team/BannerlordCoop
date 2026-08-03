using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.LordNeedsHorsesIssueCreationPatch"/>/
/// <see cref="Handlers.LordNeedsHorsesIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue"/> - the survey's "no MBRandom" characterization
/// was wrong: the constructor rolls <c>_mountObjectToBeDelivered</c> via <c>MBList&lt;ItemObject&gt;.GetRandomElement()</c>
/// (a genuine creation-time dice roll, the same shape as Village Needs Crafting Materials' requested-item
/// roll), and ALSO derives <c>_numMountsToBeDelivered</c>/<c>_mountValuePerUnit</c> in that same constructor -
/// the former from <c>IssueDifficultyMultiplier</c> (per-client at creation time, same as every Village Needs
/// Tools field), the latter from the (locally, independently rolled) mount item's <c>Value</c>. All three are
/// captured once at creation and force-written on every other peer's replicated instance - no accept-time
/// re-derivation is needed (<c>GenerateIssueQuest</c> only ever reads these three already-frozen fields plus
/// the computed <c>RewardGold</c>/<c>AlternativeSolutionGoldRequirement</c> properties, both pure functions of
/// them), so the quest-solution and alternative-solution accept paths both ride the fully generic mirror (see
/// <c>GenericAcceptMirrorIssueTypes</c>) unchanged.
/// </summary>
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

/// <inheritdoc cref="ILordNeedsHorsesIssueInterface"/>
public class LordNeedsHorsesIssueInterface : ILordNeedsHorsesIssueInterface
{
    private static readonly FieldInfo MountObjectField =
        AccessTools.Field(typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue), "_mountObjectToBeDelivered");
    private static readonly FieldInfo NumMountsField =
        AccessTools.Field(typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue), "_numMountsToBeDelivered");
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

        mountObjectToBeDelivered = (ItemObject)MountObjectField.GetValue(issue);
        numMountsToBeDelivered = (int)NumMountsField.GetValue(issue);
        mountValuePerUnit = (int)MountValuePerUnitField.GetValue(issue);
        return mountObjectToBeDelivered != null;
    }

    public LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue ConstructReplicated(
        Hero owner, ItemObject mountObjectToBeDelivered, int numMountsToBeDelivered, int mountValuePerUnit)
    {
        // The public ctor is the only way to build one, and it independently rolls _mountObjectToBeDelivered,
        // re-derives _numMountsToBeDelivered from (per-client) IssueDifficultyMultiplier, and derives
        // _mountValuePerUnit from its own (wrongly, locally-rolled) item. Build it normally for everything
        // else it sets up, then force all three fields to the server's authoritative values.
        var issue = new LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue(owner);

        MountObjectField.SetValue(issue, mountObjectToBeDelivered);
        NumMountsField.SetValue(issue, numMountsToBeDelivered);
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
