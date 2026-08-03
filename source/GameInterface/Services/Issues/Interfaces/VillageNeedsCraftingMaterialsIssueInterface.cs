using Common.Util;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection/publicized-field access <see cref="Patches.VillageNeedsCraftingMaterialsIssueCreationPatch"/>,
/// <see cref="Patches.VillageNeedsCraftingMaterialsAcceptancePatches"/> and
/// <see cref="Handlers.VillageNeedsCraftingMaterialsIssueHandler"/> need to capture and authoritatively
/// replicate a <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue"/> -
/// mirrors <see cref="VillageNeedsToolsIssueInterface"/>'s shape closely, but NOT identically, because this
/// issue type's vanilla shape genuinely differs in two load-bearing ways:
///
/// 1. Creation rolls one field (<c>_requestedItem</c>, via <c>MBRandom.RandomInt(0,2)</c> between the two iron
///    ingot variants) - the first genuine creation-time dice roll in this quest family (Tools' fields are all
///    deterministic derivations, nothing to force there). No exchange-item/barter branch exists for this issue
///    type at all (always pays gold), so there is no analogue to Tools' <c>_exchangeItem</c>/
///    <c>_numberOfExchangeItem</c> to carry.
///
/// 2. Unlike Tools, <c>_numberOfRequestedItem</c> (a computed property with NO backing field - there is
///    nothing to force on the Issue itself) and <c>RewardGold</c> are NOT frozen at creation - both are
///    re-derived at ACCEPT time, inside <c>GenerateIssueQuest</c>, from <c>IssueDifficultyMultiplier</c> AND
///    live <c>MarketData</c>/world-average-price lookups - all genuinely per-client-divergent. Every peer's own
///    <see cref="MirrorQuestAccepted"/> replay of <c>IssueManager.StartIssueQuest</c> independently re-derives
///    these onto its OWN <c>VillageNeedsCraftingMaterialsIssueQuest</c> instance, so simply replaying the real
///    method (the way Tools' equivalent gets away with doing nothing further) would bake a different
///    required-item-count/reward into each peer's own quest object - a real desync. The fix: whichever
///    machine's accept is genuine/authoritative (the host's own accept, or the server's own replay of a
///    client-arbitrated accept - see <see cref="Handlers.VillageNeedsCraftingMaterialsIssueHandler"/>) captures
///    what it actually baked via <see cref="TryCaptureQuestFields"/>, and every OTHER peer's
///    <see cref="MirrorQuestAccepted"/> force-writes those exact values onto its own quest object - critically
///    including forcing them back onto the ORIGINAL ACCEPTER'S OWN client too when a remote client accepted,
///    since that machine's own locally-computed values can't be trusted as authoritative (same reasoning this
///    project already applies to never trusting a client-claimed ControllerId).
///
/// Known, accepted cosmetic gap (documented rather than silently left as a surprise): <c>QuestAcceptedConsequences</c>
/// - which bakes a <c>JournalLog</c> (<c>_playerAcceptedQuestLog</c>) recording the required count as its
/// <c>Range</c>, plus a one-time "asked you to bring N ingots" log line - runs synchronously inside the
/// accepter's own live <c>OfferDialogFlow.Consequence</c>, unavoidably BEFORE any server round trip can
/// resolve. <see cref="MirrorQuestAccepted"/> also force-corrects <c>Range</c> once the authoritative values
/// arrive (same reflection technique as everything else here), so the progress-bar denominator ends up
/// correct - but the historical prose log text itself was already generated with the pre-correction number and
/// can't be un-said. This is a real, deterministic (not probabilistic) one-time journal-text quirk on the
/// accepter's own client whenever their local roll differed from the server's canonical one; harmless to
/// gameplay (the actual turn-in gate reads live fields, not this stale text).
/// </summary>
public interface IVillageNeedsCraftingMaterialsIssueInterface : IGameAbstraction
{
    /// <summary>Reads the one rolled field off an already-constructed issue (direct field access - see
    /// TaleWorlds.CampaignSystem's whole-assembly Publicize in GameInterface.csproj). Returns false if the
    /// issue has no requested item, which should never happen for a real instance.</summary>
    bool TryCaptureFields(
        VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue,
        out ItemObject requestedItem);

    /// <summary>
    /// Builds a <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue"/> via
    /// its normal (one-argument) constructor - which independently rolls its own <c>_requestedItem</c> via
    /// <c>SelectCraftingMaterial()</c> - then overwrites that single `readonly` field (hence the reflection)
    /// with the server's authoritative roll so every client ends up with the same requested material. Does not
    /// register it with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue ConstructReplicated(Hero owner, ItemObject requestedItem);

    /// <summary>
    /// Registers an already-built, already-forced issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead
    /// of constructing (and re-rolling) a new one - same technique as
    /// <see cref="VillageNeedsToolsIssueInterface.RegisterReplicated"/>.
    /// </summary>
    void RegisterReplicated(Hero owner, VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue);

    /// <summary>
    /// Bare, uncorrected replay of <see cref="IssueManager.StartIssueQuest"/> for a hero whose issue is still
    /// <c>IsOngoingWithoutQuest</c>, run under <see cref="AllowedThread"/>. Used ONLY by the server while
    /// arbitrating a client's accept request: the server has no authoritative amount/reward to force-write yet
    /// - replaying this IS what produces them (the server's own roll is authoritative by definition) - so the
    /// server calls this bare method first, then reads back what it produced via
    /// <see cref="TryCaptureQuestFields"/> before broadcasting. Every OTHER peer instead uses
    /// <see cref="MirrorQuestAccepted"/>, which wraps this same replay together with the force-write. A no-op
    /// if the issue is no longer <c>IsOngoingWithoutQuest</c>.
    /// </summary>
    void ReplayQuestAccepted(Hero owner);

    /// <summary>
    /// Reads back <c>_requestedItemAmount</c>/<c>RewardGold</c> off <paramref name="owner"/>'s current
    /// <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest"/> (direct
    /// field access for both - see the interface's own type doc comment on Publicize; <c>RewardGold</c> is
    /// already a public field on <c>QuestBase</c>). Returns false if the hero has no quest of this type yet.
    /// Called right after a GENUINE accept (either this machine's own live conversation, or the server's own
    /// <see cref="ReplayQuestAccepted"/>) to capture what was just authoritatively baked, for broadcasting.
    /// </summary>
    bool TryCaptureQuestFields(Hero owner, out int requestedItemAmount, out int rewardGold);

    /// <summary>
    /// The central mechanism (see the type doc comment): ensures <paramref name="owner"/> has a real quest
    /// object (replaying <see cref="ReplayQuestAccepted"/> if it doesn't yet), then force-writes
    /// <paramref name="requestedItemAmount"/>/<paramref name="rewardGold"/> onto it - and, if this machine's own
    /// <c>_playerAcceptedQuestLog</c> was already baked (i.e. this IS the machine whose live conversation
    /// genuinely accepted), also corrects that log's <c>Range</c> to match. Idempotent: safe to call again with
    /// the same values (a resend, or the server's own broadcast echoing back to itself), and safe to call on
    /// the machine whose own roll already happened to be correct (a same-value no-op). A no-op entirely if
    /// <paramref name="owner"/>'s issue isn't a <c>VillageNeedsCraftingMaterialsIssue</c>.
    /// </summary>
    void MirrorQuestAccepted(Hero owner, int requestedItemAmount, int rewardGold);

    /// <summary>
    /// Forces just the parent Issue's state to <c>SolvingWithAlternativeSolution</c> on a peer OTHER than the
    /// one that actually accepted - identical reasoning/technique to
    /// <see cref="VillageNeedsToolsIssueInterface.MirrorAlternativeAccepted"/> (see that method's doc comment
    /// for the full derivation, including why <c>AlternativeSolutionReturnTimeForTroops</c> is deliberately
    /// left alone). No amount/reward correction needed here - see
    /// <see cref="Messages.VillageCraftingIssueAlternativeAcceptTriggered"/>'s doc comment for why that path has
    /// no cross-peer divergence to fix. A no-op if the issue is no longer <c>IsOngoingWithoutQuest</c>.
    /// </summary>
    void MirrorAlternativeAccepted(Hero owner);

    /// <summary>
    /// Rolls a losing peer's own already-applied (optimistic) local acceptance back after the server tells it
    /// another peer won the same-issue accept race. Same shape and same fix as
    /// <see cref="VillageNeedsToolsIssueInterface.RejectAcceptance"/> (reuses <c>IssueBase.CompleteIssueWithCancel()</c>,
    /// which needs no type-specific handling) - duplicated here rather than shared so this feature stays
    /// self-contained (see <see cref="VillageNeedsToolsIssueOwnershipPersistencePatches"/>'s doc comment on why
    /// an undocumented cross-feature dependency is worth avoiding). Critically, this MUST also check whether
    /// an owner is already recorded (see the implementation's own comment) before rolling back - the winning
    /// accept's own <c>SendAll</c> reaches every client, including the one whose competing request is about to
    /// be rejected, so by the time this peer's own rejection arrives it may have already correctly mirrored
    /// the real winner's quest. Found by
    /// RequestVillageCraftingIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer
    /// in E2E.Tests/Services/Issues/VillageNeedsCraftingMaterialsIssueTests.cs, the exact same bug shape
    /// VillageNeedsToolsIssueTests' equivalent test found in Tools - this fix was never ported over when this
    /// file was written.
    /// </summary>
    void RejectAcceptance(Hero owner);

    /// <summary>
    /// Genuinely triggers <c>IssueBase.CompleteIssueWithAlternativeSolution()</c> for <paramref name="owner"/>'s
    /// issue, but ONLY if the local peer is this issue's recorded owner - same shape as
    /// <see cref="VillageNeedsToolsIssueInterface.TryTriggerOwnedAlternativeSolutionCompletion"/>, reusing the
    /// same shared <c>VillageNeedsToolsIssueOwnership</c> registry and
    /// <c>IssueManagerQuestCompletedReasonCapture.PendingReasons</c> choke point. Deterministic success carries
    /// over identically: this issue type's <c>AlternativeSolutionScaleFlags</c> is also
    /// <c>AlternativeSolutionScaleFlag.Duration</c> only (confirmed by decompile), so
    /// <c>AlternativeSolutionHasFailureRisk</c> is always false here too. Returns false (no-op) if there's
    /// nothing to trigger.
    /// </summary>
    bool TryTriggerOwnedAlternativeSolutionCompletion(Hero owner);
}

/// <inheritdoc cref="IVillageNeedsCraftingMaterialsIssueInterface"/>
public class VillageNeedsCraftingMaterialsIssueInterface : IVillageNeedsCraftingMaterialsIssueInterface
{
    private static readonly FieldInfo RequestedItemField =
        AccessTools.Field(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue), "_requestedItem");
    private static readonly FieldInfo RequestedItemAmountField =
        AccessTools.Field(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest), "_requestedItemAmount");
    private static readonly FieldInfo RewardGoldField =
        AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));
    private static readonly FieldInfo PlayerAcceptedQuestLogField =
        AccessTools.Field(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest), "_playerAcceptedQuestLog");
    private static readonly FieldInfo JournalLogRangeField =
        AccessTools.Field(typeof(JournalLog), nameof(JournalLog.Range));

    // IssueBase.IssueState is a private nested enum, reflected once statically - same targets
    // VillageNeedsToolsIssueInterface already reflects, but private to that class, so duplicated here rather
    // than shared (keeps each issue type's interface self-contained - see this type's own doc comment).
    private static readonly Type IssueStateEnumType =
        AccessTools.Inner(typeof(IssueBase), "IssueState");
    private static readonly FieldInfo IssueStateField =
        AccessTools.Field(typeof(IssueBase), "_issueState");
    private static readonly object SolvingWithAlternativeSolutionStateValue =
        Enum.Parse(IssueStateEnumType, "SolvingWithAlternativeSolution");
    private static readonly PropertyInfo IsTriedToSolveBeforeProperty =
        AccessTools.Property(typeof(IssueBase), nameof(IssueBase.IsTriedToSolveBefore));

    public bool TryCaptureFields(
        VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue,
        out ItemObject requestedItem)
    {
        requestedItem = null;
        if (issue == null) return false;

        requestedItem = issue._requestedItem;
        return requestedItem != null;
    }

    public VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue ConstructReplicated(Hero owner, ItemObject requestedItem)
    {
        // The public ctor is the only way to build one, and it independently rolls _requestedItem via
        // SelectCraftingMaterial() (an MBRandom.RandomInt(0,2) coin flip between the two iron ingot variants).
        // Build it the normal way for everything else it sets up, then force this one `readonly` field to the
        // server's authoritative roll.
        var issue = new VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue(owner);

        RequestedItemField.SetValue(issue, requestedItem);

        return issue;
    }

    public void RegisterReplicated(Hero owner, VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue), IssueBase.IssueFrequency.Rare);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out int requestedItemAmount, out int rewardGold)
    {
        requestedItemAmount = 0;
        rewardGold = 0;

        if (owner?.Issue?.IssueQuest is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest quest) return false;

        requestedItemAmount = quest._requestedItemAmount;
        rewardGold = quest.RewardGold;
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, int requestedItemAmount, int rewardGold)
    {
        if (owner?.Issue is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest quest) return;

            RequestedItemAmountField.SetValue(quest, requestedItemAmount);
            RewardGoldField.SetValue(quest, rewardGold);

            // Cosmetic-gap correction (see the type doc comment): only ever non-null on the machine whose own
            // live conversation genuinely accepted (QuestAcceptedConsequences already ran there, synchronously,
            // before this correction could possibly arrive) - null everywhere else, so this is skipped there.
            if (PlayerAcceptedQuestLogField.GetValue(quest) is JournalLog log)
            {
                JournalLogRangeField.SetValue(log, requestedItemAmount);
            }
        }
    }

    public void MirrorAlternativeAccepted(Hero owner)
    {
        if (owner?.Issue is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue || !issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            IssueStateField.SetValue(issue, SolvingWithAlternativeSolutionStateValue);
            IsTriedToSolveBeforeProperty.SetValue(issue, true);
            issue.IssueDueTime = CampaignTime.Never;
        }
    }

    public void RejectAcceptance(Hero owner)
    {
        if (owner?.Issue == null || owner.Issue.IsOngoingWithoutQuest) return;

        // Bug fix (found by RequestVillageCraftingIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer
        // in E2E.Tests/Services/Issues/VillageNeedsCraftingMaterialsIssueTests.cs - the exact same bug shape as
        // VillageNeedsToolsIssueInterface.RejectAcceptance, see that method's comment for the full derivation):
        // the winning accept's SendAll reaches EVERY client, including the one whose own competing request is
        // about to be rejected - so by the time this losing requester's own NetworkVillageCraftingIssueAcceptRejected
        // arrives, it may already have correctly mirrored the ACTUAL winner's accept via
        // Handle_NetworkVillageCraftingIssueQuestAccepted (which always records ownership). The old guard here
        // (IsOngoingWithoutQuest alone) cannot tell "this is still MY OWN unconfirmed optimistic accept" apart
        // from "this is already the legitimate mirrored quest of whoever actually won" - both look identical -
        // so it was cancelling the correctly-mirrored winning quest out from under this peer. Ownership is
        // only ever recorded from that same authoritative broadcast, never optimistically by this peer's own
        // accept trigger, so "no owner recorded yet" is exactly the signal that whatever is sitting in
        // owner.Issue right now is still this peer's own, never-mirrored, optimistic accept - safe to roll
        // back. Once an owner IS recorded, a legitimate mirror already superseded it and must be left alone.
        if (VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out _)) return;

        using (new AllowedThread())
        {
            owner.Issue.CompleteIssueWithCancel();
        }
    }

    public bool TryTriggerOwnedAlternativeSolutionCompletion(Hero owner)
    {
        if (owner?.Issue is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue) return false;
        if (!issue.IsSolvingWithAlternative || !issue.AlternativeSolutionReturnTimeForTroops.IsPast) return false;
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(owner)) return false;

        // Seed the reason IssueFinalizedPatches.Postfix will pick up when IssueFinalized() cascades below -
        // reuses the same shared choke point Tools' equivalent does.
        Patches.IssueManagerQuestCompletedReasonCapture.PendingReasons[owner] = VillageIssueFinalizeReason.AlternativeSolutionSuccess;

        issue.CompleteIssueWithAlternativeSolution(); // genuine call - NOT under AllowedThread, this is the real trigger
        return true;
    }
}
