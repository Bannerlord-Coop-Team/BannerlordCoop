using Common.Util;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the access <see cref="Patches.RevenueFarmingIssueCreationPatch"/>/
/// <see cref="Patches.RevenueFarmingQuestAcceptancePatch"/>/<see cref="Handlers.RevenueFarmingIssueHandler"/>
/// need to capture and authoritatively replicate a
/// <see cref="RevenueFarmingIssueBehavior.RevenueFarmingIssue"/>/<see cref="RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest"/>.
///
/// CREATION: the Issue ctor takes <c>(Hero issueOwner, Settlement targetSettlement)</c> DIRECTLY, and
/// <c>_targetSettlement</c> is a plain (non-readonly) private field - exactly the same "no reflection needed at
/// all" shape as <c>ArtisanOverpricedGoodsIssueInterface</c>/<c>SmugglersIssueInterface</c>: the real public
/// ctor accepts the captured value directly, and <see cref="TryCaptureFields"/> can read it back with a direct
/// (Publicize-enabled) field access.
///
/// ACCEPT TIME (the genuinely bespoke mechanic this type needs - see
/// <see cref="Messages.RevenueFarmingQuestAcceptTriggered"/>'s doc comment for the full derivation):
/// <c>GenerateIssueQuest</c> re-derives <c>_revenueVillages</c> LIVE from <c>_targetSettlement.BoundVillages</c>,
/// filtered by each village's CURRENT <c>IsUnderRaid</c>/<c>IsRaided</c> state at that exact instant - a real
/// per-client-divergence risk if raid timing differs even slightly across peers - and <c>_totalRequestedDenars</c>
/// is computed differently (and so, in general, to a DIFFERENT number) than the Issue-level
/// <c>TotalRequestedDenars</c> preview property, due to integer-division-on-differing-groupings. So this type is
/// NOT in <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/> - a bare replay of
/// <c>IssueManager.StartIssueQuest</c> is not safe to trust as-is on a mirroring peer; whichever peer's accept is
/// genuine must capture what it actually baked (<see cref="TryCaptureQuestFields"/>) and every other peer force-
/// corrects onto it (<see cref="MirrorQuestAccepted"/>) - the same capture-once-force-write-plus-multi-field
/// pattern <c>VillageNeedsCraftingMaterialsIssueInterface</c> established, generalized here from two scalar
/// fields to one scalar (<c>_totalRequestedDenars</c>, a plain internal - i.e. Publicize-mutable - field, read
/// directly but still written via reflection for uniformity with every other force-write in this family) plus a
/// whole `readonly` LIST (<c>_revenueVillages</c>, which - like every other `readonly` field in this family -
/// needs reflection to overwrite). <c>TargetSettlement</c> (a <c>{ get; private set; }</c> property, always
/// derived in the real ctor/<c>InitializeQuestOnGameLoad</c> as <c>_revenueVillages[0].Village.Bound</c>) is
/// force-corrected alongside the list so it stays consistent with whichever village ends up at index 0 of the
/// AUTHORITATIVE list, not whatever this peer's own independently-derived list happened to produce.
///
/// <c>RevenueVillage</c>'s own public ctor already zeroes <c>CollectedAmount</c>/<c>_isDone</c>/
/// <c>EventOccurred</c>/<c>IsRaided</c>/<c>_customProgress</c> for free (see the decompiled source) - so
/// reconstructing the list from <see cref="Messages.RevenueVillageWireEntry"/> data via
/// <c>new RevenueVillage(village, targetAmount)</c> needs no further reflection to reset those fields.
///
/// This type has no alternative solution (<c>IsThereAlternativeSolution == false</c>, confirmed against the
/// decompiled source), so there is no <c>MirrorAlternativeAccepted</c> equivalent here at all - unlike
/// <c>VillageNeedsCraftingMaterialsIssueInterface</c>, which needs one for its OWN alternative-solution branch.
/// </summary>
public interface IRevenueFarmingIssueInterface : IGameAbstraction
{
    /// <summary>Reads the one ctor-arg field off an already-constructed issue (direct field read - see the
    /// type doc comment on why no reflection is needed here). Returns false if it's missing, which should
    /// never happen for a real instance.</summary>
    bool TryCaptureFields(RevenueFarmingIssueBehavior.RevenueFarmingIssue issue, out Settlement targetSettlement);

    /// <summary>
    /// Builds a <see cref="RevenueFarmingIssueBehavior.RevenueFarmingIssue"/> via its real public ctor, which
    /// takes the captured settlement directly - no field-forcing/reflection needed (see the type doc comment).
    /// Does not register it with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    RevenueFarmingIssueBehavior.RevenueFarmingIssue ConstructReplicated(Hero owner, Settlement targetSettlement);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of
    /// constructing a new one - same technique as every other type in this family.
    /// </summary>
    void RegisterReplicated(Hero owner, RevenueFarmingIssueBehavior.RevenueFarmingIssue issue);

    /// <summary>
    /// Bare, uncorrected replay of <see cref="IssueManager.StartIssueQuest"/> for a hero whose issue is still
    /// <c>IsOngoingWithoutQuest</c>, run under <see cref="AllowedThread"/>. Used ONLY by the server while
    /// arbitrating a client's accept request: the server has no authoritative villages/total yet - replaying
    /// this IS what produces them (the server's own live-derived list is authoritative by definition) - so the
    /// server calls this bare method first, then reads back what it produced via <see cref="TryCaptureQuestFields"/>
    /// before broadcasting. Every OTHER peer instead uses <see cref="MirrorQuestAccepted"/>, which wraps this
    /// same replay together with the force-write. A no-op if the issue is no longer <c>IsOngoingWithoutQuest</c>.
    /// </summary>
    void ReplayQuestAccepted(Hero owner);

    /// <summary>
    /// Reads back <c>_totalRequestedDenars</c> (direct field access) and the current <c>RevenueVillages</c>
    /// (the real public property - already exposes <c>_revenueVillages</c> directly, no reflection needed for
    /// this read) off <paramref name="owner"/>'s current <see cref="RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest"/>,
    /// projected to <c>(Settlement, TargetAmount)</c> pairs (<c>RevenueVillage.Village.Settlement</c>, not
    /// <c>RevenueVillage.Village</c> itself - see the type doc comment on why <c>Village</c> can't travel the
    /// wire directly). Returns false if the hero has no quest of this type yet, or if it somehow has zero
    /// villages (should never happen for a real instance - <c>IssueStayAliveConditions</c> already guarantees
    /// at least one is available before a quest can ever be started).
    /// </summary>
    bool TryCaptureQuestFields(Hero owner, out int totalRequestedDenars, out IReadOnlyList<(Settlement Settlement, int TargetAmount)> villages);

    /// <summary>
    /// The central mechanism (see the type doc comment): ensures <paramref name="owner"/> has a real quest
    /// object (replaying <see cref="ReplayQuestAccepted"/> if it doesn't yet), then force-writes
    /// <paramref name="totalRequestedDenars"/>/a freshly-reconstructed <c>_revenueVillages</c> list (from
    /// <paramref name="villages"/>, re-deriving each <c>Village</c> via <c>Settlement.Village</c>) onto it, and
    /// corrects <c>TargetSettlement</c> to match the corrected list's own index 0. Idempotent: safe to call
    /// again with the same values (a resend, or the server's own broadcast echoing back to itself). A no-op
    /// entirely if <paramref name="owner"/>'s issue isn't a <c>RevenueFarmingIssue</c>, or if
    /// <paramref name="villages"/> is empty or resolves to zero usable entries (e.g. every settlement id failed
    /// to resolve) - never force-writes a quest into the same broken empty-list state vanilla's own ctor would
    /// index-out-of-range on.
    /// </summary>
    void MirrorQuestAccepted(Hero owner, int totalRequestedDenars, IReadOnlyList<(Settlement Settlement, int TargetAmount)> villages);

    /// <summary>
    /// Rolls a losing peer's own already-applied (optimistic) local acceptance back after the server tells it
    /// another peer won the same-issue accept race. Same shape and same already-fixed reasoning as
    /// <c>VillageNeedsCraftingMaterialsIssueInterface.RejectAcceptance</c> (reuses <c>IssueBase.CompleteIssueWithCancel()</c>
    /// and the "no owner recorded yet" guard against rolling back an already-correctly-mirrored winning accept -
    /// see that method's doc comment for the full derivation of why the guard matters) - duplicated here rather
    /// than shared so this feature stays self-contained, matching every other type in this family.
    /// </summary>
    void RejectAcceptance(Hero owner);
}

/// <inheritdoc cref="IRevenueFarmingIssueInterface"/>
public class RevenueFarmingIssueInterface : IRevenueFarmingIssueInterface
{
    private static readonly FieldInfo TotalRequestedDenarsField =
        AccessTools.Field(typeof(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest), "_totalRequestedDenars");
    private static readonly FieldInfo RevenueVillagesField =
        AccessTools.Field(typeof(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest), "_revenueVillages");
    private static readonly PropertyInfo TargetSettlementProperty =
        AccessTools.Property(
            typeof(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest),
            nameof(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest.TargetSettlement));

    public bool TryCaptureFields(RevenueFarmingIssueBehavior.RevenueFarmingIssue issue, out Settlement targetSettlement)
    {
        targetSettlement = null;
        if (issue == null) return false;

        targetSettlement = issue._targetSettlement;
        return targetSettlement != null;
    }

    public RevenueFarmingIssueBehavior.RevenueFarmingIssue ConstructReplicated(Hero owner, Settlement targetSettlement)
    {
        return new RevenueFarmingIssueBehavior.RevenueFarmingIssue(owner, targetSettlement);
    }

    public void RegisterReplicated(Hero owner, RevenueFarmingIssueBehavior.RevenueFarmingIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(RevenueFarmingIssueBehavior.RevenueFarmingIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not RevenueFarmingIssueBehavior.RevenueFarmingIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out int totalRequestedDenars, out IReadOnlyList<(Settlement Settlement, int TargetAmount)> villages)
    {
        totalRequestedDenars = 0;
        villages = null;

        if (owner?.Issue?.IssueQuest is not RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest quest) return false;

        totalRequestedDenars = quest._totalRequestedDenars;
        var captured = quest.RevenueVillages?
            .Where(rv => rv?.Village?.Settlement != null)
            .Select(rv => (rv.Village.Settlement, rv.TargetAmount))
            .ToList();

        villages = captured;
        return captured is { Count: > 0 };
    }

    public void MirrorQuestAccepted(Hero owner, int totalRequestedDenars, IReadOnlyList<(Settlement Settlement, int TargetAmount)> villages)
    {
        if (owner?.Issue is not RevenueFarmingIssueBehavior.RevenueFarmingIssue) return;
        if (villages == null || villages.Count == 0) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest quest) return;

            var revenueVillages = villages
                .Where(v => v.Settlement?.Village != null)
                .Select(v => new RevenueFarmingIssueBehavior.RevenueVillage(v.Settlement.Village, v.TargetAmount))
                .ToList();
            if (revenueVillages.Count == 0) return;

            RevenueVillagesField.SetValue(quest, revenueVillages);
            TotalRequestedDenarsField.SetValue(quest, totalRequestedDenars);
            TargetSettlementProperty.SetValue(quest, revenueVillages[0].Village.Bound);
        }
    }

    public void RejectAcceptance(Hero owner)
    {
        if (owner?.Issue == null || owner.Issue.IsOngoingWithoutQuest) return;

        // Same guard/reasoning as VillageNeedsCraftingMaterialsIssueInterface.RejectAcceptance (see that
        // method's doc comment): the winning accept's own SendAll reaches every client, including the one
        // whose competing request is about to be rejected - "no owner recorded yet" is exactly the signal that
        // whatever is sitting in owner.Issue right now is still this peer's own, never-mirrored, optimistic
        // accept, safe to roll back. Once an owner IS recorded, a legitimate mirror already superseded it.
        if (VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out _)) return;

        using (new AllowedThread())
        {
            owner.Issue.CompleteIssueWithCancel();
        }
    }
}
