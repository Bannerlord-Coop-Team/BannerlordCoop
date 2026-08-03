using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.NearbyBanditBaseIssueCreationPatch"/>/
/// <see cref="Handlers.NearbyBanditBaseIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue"/>. Same shape as
/// <see cref="CapturedByBountyHuntersIssueInterface"/>: the hideout pick (<c>FindSuitableHideout</c> - a
/// deterministic nearest-infested-hideout search, no MBRandom) is resolved BEFORE construction and handed in as
/// a constructor argument, so there's no independent-re-roll risk from the constructor itself - but the chosen
/// <see cref="Settlement"/> is still an object reference that must be broadcast (not re-derived locally), since
/// "nearest by distance among currently-infested hideouts" can drift from unrelated per-client hideout state
/// (which hideouts are infested changes over time) even though the search itself is deterministic.
///
/// <c>_issueSettlement</c> (the OTHER private field, set in <c>AfterIssueCreation</c> to
/// <c>IssueOwner.CurrentSettlement</c>) is deliberately NOT captured here - it has no <c>[SaveableField]</c>
/// attribute... actually it DOES (SaveableField(101)), unlike Art of the Trade's/Rural Notable's analogous
/// settlement-derived fields, but its value is still a pure, already-synced function of the (already-replicated)
/// <c>IssueOwner.CurrentSettlement</c> at the moment <c>AfterIssueCreation</c> runs on every peer's own replayed
/// construction (immediately after <c>CreateNewIssue</c>'s real, unconditional call to it) - so every peer
/// independently derives the identical value without needing it broadcast.
///
/// GenerateIssueQuest reads only <c>_targetHideout</c>/<c>_issueSettlement</c> (both covered above) plus the
/// constant <c>RewardGold</c> (hardcoded 3000, no roll) - so once creation is captured/forced, the quest-solution
/// accept path rides the fully generic mirror (see <see cref="GenericAcceptMirrorIssueTypes"/>) unchanged, same
/// as Captured by Bounty Hunters.
/// </summary>
public interface INearbyBanditBaseIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue issue, out Settlement targetHideout);

    NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue ConstructReplicated(Hero owner, Settlement targetHideout);

    void RegisterReplicated(Hero owner, NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue issue);
}

/// <inheritdoc cref="INearbyBanditBaseIssueInterface"/>
public class NearbyBanditBaseIssueInterface : INearbyBanditBaseIssueInterface
{
    private static readonly FieldInfo TargetHideoutField =
        AccessTools.Field(typeof(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue), "_targetHideout");

    public bool TryCaptureFields(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue issue, out Settlement targetHideout)
    {
        targetHideout = null;
        if (issue == null) return false;

        targetHideout = (Settlement)TargetHideoutField.GetValue(issue);
        return targetHideout != null;
    }

    public NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue ConstructReplicated(Hero owner, Settlement targetHideout)
    {
        // The real ctor already takes the hideout directly (no independent roll to override) - construct
        // normally. _issueSettlement is set for us, identically on every peer, by the real AfterIssueCreation
        // override that CreateNewIssue unconditionally calls (see the type doc comment).
        return new NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue(owner, targetHideout);
    }

    public void RegisterReplicated(Hero owner, NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
