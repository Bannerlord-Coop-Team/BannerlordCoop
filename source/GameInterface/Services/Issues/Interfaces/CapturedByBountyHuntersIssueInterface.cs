using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.CapturedByBountyHuntersIssueCreationPatch"/>/
/// <see cref="Handlers.CapturedByBountyHuntersIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue"/>. Unlike Village Needs Tools'
/// item rolls, the hideout pick itself (<c>FindSuitableHideout</c> - a deterministic nearest-hideout search, no
/// MBRandom) is resolved BEFORE construction and handed in as a constructor argument (via
/// <c>PotentialIssueData.RelatedObject</c>), so there's no independent-re-roll risk from the constructor
/// itself the way Tools/Lord Needs Horses have - but the chosen <see cref="Settlement"/> is still an object
/// reference that must be broadcast (not re-derived locally) so every peer's replicated copy points at the
/// exact same settlement, since "nearest by distance" could tie or drift from unrelated per-client state.
///
/// The genuine wrinkle the survey missed for this type: its quest-solution resolution is NOT dialogue-driven
/// at all (<c>RewardGold</c> is a fixed 3000, no accept-time re-derivation, so the accept path rides the fully
/// generic mirror - see <c>GenericAcceptMirrorIssueTypes</c>) - it resolves via a <c>MapEventEnded</c> listener
/// reacting to whichever peer's own local combat happens to end against the hideout. Since every peer's own
/// mirrored <c>CapturedByBountyHuntersIssueQuest</c> independently listens for this, a non-owner who
/// separately/coincidentally clears the same hideout (or fights alongside the true owner in the same battle)
/// would otherwise also collect the reward - the same ownership gap Bug 1 fixed for Tools, just triggered by a
/// map event instead of a dialogue option. See <see cref="Patches.CapturedByBountyHuntersOwnershipGatePatches"/>.
/// </summary>
public interface ICapturedByBountyHuntersIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue issue, out Settlement hideout);

    CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue ConstructReplicated(Hero owner, Settlement hideout);

    void RegisterReplicated(Hero owner, CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue issue);
}

/// <inheritdoc cref="ICapturedByBountyHuntersIssueInterface"/>
public class CapturedByBountyHuntersIssueInterface : ICapturedByBountyHuntersIssueInterface
{
    private static readonly FieldInfo HideoutField =
        AccessTools.Field(typeof(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue), "_hideout");

    public bool TryCaptureFields(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue issue, out Settlement hideout)
    {
        hideout = null;
        if (issue == null) return false;

        hideout = (Settlement)HideoutField.GetValue(issue);
        return hideout != null;
    }

    public CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue ConstructReplicated(Hero owner, Settlement hideout)
    {
        // The real ctor already takes the hideout directly (no independent roll to override) - construct
        // normally. Kept as an explicit force-write anyway (rather than trusting the ctor argument alone) for
        // defense in depth and to match every other type's replication shape.
        var issue = new CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue(owner, hideout);
        HideoutField.SetValue(issue, hideout);
        return issue;
    }

    public void RegisterReplicated(Hero owner, CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
