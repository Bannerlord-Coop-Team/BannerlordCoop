using Common.Util;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.GangLeaderNeedsToOffloadStolenGoodsIssueCreationPatch"/>,
/// <see cref="Patches.GangLeaderNeedsToOffloadStolenGoodsAcceptancePatches"/> and
/// <see cref="Handlers.GangLeaderNeedsToOffloadStolenGoodsIssueHandler"/> need to capture and authoritatively
/// replicate a <see cref="GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue"/>.
///
/// CREATION: the Issue ctor takes <c>(Hero issueOwner, Settlement hideout)</c> - the hideout itself is picked
/// server-side by <c>FindSuitableHideout</c> (deterministic nearest-infested-hideout search, same shape as
/// <see cref="NearbyBanditBaseIssueInterface"/>'s hideout pick) and handed in, so no independent-re-roll risk
/// from that argument - but it's still an object reference that must be broadcast (which hideouts are infested
/// changes over time, so a client's own independent search could drift). The ctor body ALSO independently rolls
/// <c>_randomForStolenTradeGood</c> via a genuine <c>MBRandom.RandomInt(0, 4)</c> - the first real dice roll in
/// this type. <c>CounterOfferHero</c> is set neither by the ctor nor passed in, but by an
/// <c>AfterIssueCreation()</c> override (<c>FirstOrDefault</c> over the owner's settlement's Notables) that
/// <c>IssueManager.CreateNewIssue</c>'s own bookkeeping calls AFTER the factory returns the issue (see
/// <c>IssueBase.AfterCreation</c> in the decompiled source) - critically, this means forcing
/// <c>CounterOfferHero</c> BEFORE <see cref="RegisterReplicated"/> ever calls <c>CreateNewIssue</c> would be
/// silently overwritten by that peer's own independent <c>AfterIssueCreation()</c> run. <see cref="RegisterReplicated"/>
/// therefore forces it AFTER <c>CreateNewIssue</c> returns instead - the opposite order from every other
/// creation-capture type in this project (compare
/// <see cref="Interfaces.IArtisanCantSellProductsAtAFairPriceIssueInterface"/>'s doc comment, which does NOT
/// have this hazard because none of its four fields are touched by an <c>AfterIssueCreation</c> override).
///
/// ACCEPT TIME (the genuinely novel, price-sync-critical piece the task flagged): unlike every other type in
/// <see cref="GenericAcceptMirrorIssueTypes"/>, this type's quest-solution accept is NOT safely mirror-eligible.
/// <c>GenerateIssueQuest</c> passes <c>StolenTradeGood</c>/<c>_issueHideout</c>/<c>StolenTradeGoodPrice</c>/
/// <c>RewardGold</c>/<c>StolenTradeGoodAmount</c>/<c>CounterOfferHero</c> into the Quest ctor - and
/// <c>StolenTradeGoodAmount</c>/<c>StolenTradeGoodPrice</c>/<c>RewardGold</c> (all Issue-level, computed
/// properties with NO backing field) are re-derived EVERY evaluation from
/// <c>base.IssueOwner.CurrentSettlement.Town.GetItemPrice(StolenTradeGood)</c> (live, per-client market data)
/// AND <c>base.IssueDifficultyMultiplier</c> (re-rolled fresh from <c>Campaign.Current.PlayerProgress</c> the
/// instant <c>IssueBase.StartIssueWithQuest</c> runs - confirmed against the decompiled <c>IssueBase</c> source,
/// same mechanism <see cref="Interfaces.IVillageNeedsCraftingMaterialsIssueInterface"/>'s doc comment already
/// proved for Crafting Materials) - genuinely per-client-divergent on BOTH axes, worse than Crafting Materials'
/// single-axis divergence. Confirmed-real bug found independently of the task's own flag: the Quest ctor
/// computes <c>_counterOfferGold</c> via ITS OWN second, independent <c>Town.GetItemPrice(_stolenTradeGood)</c>
/// read (<c>Round(questGiver.CurrentSettlement.Town.GetItemPrice(_stolenTradeGood) * _stolenTradeGoodAmount *
/// 0.4f)</c>) instead of deriving it from the ALREADY-computed <c>stolenTradeGoodPrice</c>/<c>rewardGold</c> ctor
/// parameters it was just handed - literally the same <c>price * 0.4f * amount</c> formula
/// <c>RewardGold</c>(Issue) already computed, re-evaluated a second time. Even after every OTHER field here is
/// captured/forced, replaying the real ctor on a mirror peer would independently re-derive
/// <c>_counterOfferGold</c> from THAT peer's own live price a second time, diverging from the accepter's value
/// regardless. The fix follows the exact pattern <see cref="Interfaces.IVillageNeedsCraftingMaterialsIssueInterface"/>
/// pioneered: capture what the genuine accepter's own real ctor call actually baked
/// (<see cref="TryCaptureQuestFields"/>), broadcast it, and force-write those exact values onto every OTHER
/// peer's mirror copy - and back onto the accepter's own copy too - via <see cref="MirrorQuestAccepted"/>,
/// eliminating the second-read divergence for <c>_counterOfferGold</c> the same way it eliminates it for
/// <c>_stolenTradeGoodAmount</c>/<c>_stolenTradeGoodPrice</c>/<c>RewardGold</c>.
///
/// ALTERNATIVE SOLUTION (the task's second flagged gap - "the Issue-level alt-solution payout has ZERO
/// capture-once protection at all"): <c>AlternativeSolutionEndWithSuccessConsequence</c> pays out
/// <c>RewardGold</c>/<c>StolenTradeGoodAmount</c> read LIVE off the ISSUE itself at COMPLETION time (which can
/// be days after accept, once <c>AlternativeSolutionBaseDurationInDaysInternal</c> elapses) - unlike the
/// quest-solution path, there is no <c>[SaveableField]</c> anywhere on the Issue class these could be force-
/// written onto; they are pure getters with nothing to force. This consequence only ever runs on the recorded
/// owner (see <see cref="Patches.GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches"/> /
/// <see cref="Patches.NewIssueTypesAlternativeSolutionOwnershipGatePatch"/>), so there is no CROSS-PEER
/// divergence risk in the sense every other fix in this file addresses - but the value actually paid out can
/// still silently drift from the value shown to the player in
/// <c>IssueAlternativeSolutionExplanationByIssueGiver</c> at accept time, purely from <c>Town.GetItemPrice</c>
/// fluctuating over the alternative solution's duration - the same "authoritative value must be captured once,
/// not re-derived later" principle, just applied to a single-peer computation instead of a cross-peer one.
/// Fixed the same way: <see cref="TryCaptureAlternativeSolutionFields"/> captures what the genuine accepter's
/// own <c>StartIssueWithAlternativeSolution</c> call just computed, and <see cref="MirrorAlternativeAccepted"/>
/// freezes it (via <see cref="GangLeaderStolenGoodsAlternativeSolutionFreeze"/>, since there is no field to
/// force onto - see <see cref="Patches.GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches"/>'s property-
/// getter patches that consult this freeze) on every peer, including the accepter itself.
///
/// This type deliberately has NO bespoke <c>RejectAcceptance</c> of its own - see the task's explicit
/// instruction and <see cref="Handlers.GangLeaderNeedsToOffloadStolenGoodsIssueHandler"/>'s doc comment: the
/// shared <see cref="VillageNeedsToolsIssueInterface.RejectAcceptance"/> needs no type-specific logic at all
/// (it only ever checks <c>IsOngoingWithoutQuest</c>/<see cref="VillageNeedsToolsIssueOwnership"/>), so reusing
/// it directly avoids any risk of reintroducing the accept-race bug already found and fixed three times via
/// copy-paste.
/// </summary>
public interface IGangLeaderNeedsToOffloadStolenGoodsIssueInterface : IGameAbstraction
{
    // --- Creation ---

    bool TryCaptureFields(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue issue,
        out Settlement issueHideout,
        out int randomForStolenTradeGood,
        out Hero counterOfferHero);

    GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue ConstructReplicated(
        Hero owner, Settlement issueHideout, int randomForStolenTradeGood);

    /// <summary>
    /// Registers the replicated issue, then forces <paramref name="counterOfferHero"/> AFTER
    /// <c>CreateNewIssue</c>'s own bookkeeping (including this peer's own independent
    /// <c>AfterIssueCreation()</c> run) has already completed - see the type doc comment for why the ordering
    /// matters here specifically.
    /// </summary>
    void RegisterReplicated(
        Hero owner, GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue issue, Hero counterOfferHero);

    // --- Quest-solution accept (bespoke price capture - see the type doc comment) ---

    /// <summary>Bare, uncorrected replay of <see cref="IssueManager.StartIssueQuest"/>, used ONLY by the server
    /// while arbitrating a client's accept request - the server's own roll IS authoritative, so it replays
    /// first, then reads back what it produced via <see cref="TryCaptureQuestFields"/>. A no-op if the issue is
    /// no longer <c>IsOngoingWithoutQuest</c>.</summary>
    void ReplayQuestAccepted(Hero owner);

    bool TryCaptureQuestFields(
        Hero owner,
        out int stolenTradeGoodAmount,
        out int stolenTradeGoodPrice,
        out int rewardGold,
        out int counterOfferGold);

    /// <summary>Ensures <paramref name="owner"/> has a real quest object (replaying if it doesn't yet), then
    /// force-writes all four captured fields onto it - including back onto the accepter's own copy. Idempotent
    /// and safe to call on the machine whose own roll already happened to match.</summary>
    void MirrorQuestAccepted(
        Hero owner,
        int stolenTradeGoodAmount,
        int stolenTradeGoodPrice,
        int rewardGold,
        int counterOfferGold);

    // --- Alternative-solution accept (bespoke payout freeze - see the type doc comment) ---

    bool TryCaptureAlternativeSolutionFields(Hero owner, out int stolenTradeGoodAmount, out int rewardGold);

    /// <summary>Forces the parent Issue's state to <c>SolvingWithAlternativeSolution</c> (same generic
    /// force-write every other type's <c>MirrorAlternativeAccepted</c> already does) AND freezes the captured
    /// payout amounts via <see cref="GangLeaderStolenGoodsAlternativeSolutionFreeze"/> - on every peer,
    /// including the accepter itself.</summary>
    void MirrorAlternativeAccepted(Hero owner, int stolenTradeGoodAmount, int rewardGold);
}

/// <summary>
/// Per-owner freeze of the alternative-solution payout amounts, captured once at genuine alt-accept time and
/// consulted by <see cref="Patches.GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches"/>'s
/// <c>StolenTradeGoodAmount</c>/<c>RewardGold</c> property-getter patches instead of letting
/// <c>AlternativeSolutionEndWithSuccessConsequence</c> re-derive them live at completion time - see
/// <see cref="IGangLeaderNeedsToOffloadStolenGoodsIssueInterface"/>'s doc comment. Keyed by owner Hero, same
/// convention as <see cref="VillageNeedsToolsIssueOwnership"/>/<see cref="Patches.IssueManagerQuestCompletedReasonCapture.PendingReasons"/>
/// (1 issue per hero). Drained on finalize by
/// <see cref="Patches.GangLeaderNeedsToOffloadStolenGoodsFinalizeCleanupPatch"/> so a stale entry never leaks
/// into this hero's NEXT, unrelated issue.
/// </summary>
internal static class GangLeaderStolenGoodsAlternativeSolutionFreeze
{
    private static readonly Dictionary<Hero, (int Amount, int Reward)> FrozenByOwner = new();

    public static void Freeze(Hero owner, int stolenTradeGoodAmount, int rewardGold)
    {
        if (owner == null) return;
        FrozenByOwner[owner] = (stolenTradeGoodAmount, rewardGold);
    }

    public static bool TryGetFrozenAmount(Hero owner, out int amount)
    {
        amount = 0;
        if (owner != null && FrozenByOwner.TryGetValue(owner, out var frozen))
        {
            amount = frozen.Amount;
            return true;
        }
        return false;
    }

    public static bool TryGetFrozenReward(Hero owner, out int reward)
    {
        reward = 0;
        if (owner != null && FrozenByOwner.TryGetValue(owner, out var frozen))
        {
            reward = frozen.Reward;
            return true;
        }
        return false;
    }

    public static void Clear(Hero owner)
    {
        if (owner == null) return;
        FrozenByOwner.Remove(owner);
    }
}

/// <inheritdoc cref="IGangLeaderNeedsToOffloadStolenGoodsIssueInterface"/>
public class GangLeaderNeedsToOffloadStolenGoodsIssueInterface : IGangLeaderNeedsToOffloadStolenGoodsIssueInterface
{
    private static readonly FieldInfo RandomForStolenTradeGoodField =
        AccessTools.Field(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), "_randomForStolenTradeGood");
    private static readonly FieldInfo IssueHideoutField =
        AccessTools.Field(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), "_issueHideout");
    private static readonly PropertyInfo CounterOfferHeroProperty =
        AccessTools.Property(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), nameof(IssueBase.CounterOfferHero));

    private static readonly FieldInfo StolenTradeGoodField =
        AccessTools.Field(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest), "_stolenTradeGood");
    private static readonly FieldInfo StolenTradeGoodAmountField =
        AccessTools.Field(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest), "_stolenTradeGoodAmount");
    private static readonly FieldInfo StolenTradeGoodPriceField =
        AccessTools.Field(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest), "_stolenTradeGoodPrice");
    private static readonly FieldInfo CounterOfferGoldField =
        AccessTools.Field(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest), "_counterOfferGold");
    private static readonly FieldInfo RewardGoldField =
        AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));

    // IssueBase.IssueState is a private nested enum, reflected once statically - same targets every other
    // issue-type interface in this project reflects independently (kept self-contained rather than shared -
    // see VillageNeedsCraftingMaterialsIssueInterface's own doc comment for why).
    private static readonly System.Type IssueStateEnumType =
        AccessTools.Inner(typeof(IssueBase), "IssueState");
    private static readonly FieldInfo IssueStateField =
        AccessTools.Field(typeof(IssueBase), "_issueState");
    private static readonly object SolvingWithAlternativeSolutionStateValue =
        System.Enum.Parse(IssueStateEnumType, "SolvingWithAlternativeSolution");
    private static readonly PropertyInfo IsTriedToSolveBeforeProperty =
        AccessTools.Property(typeof(IssueBase), nameof(IssueBase.IsTriedToSolveBefore));

    public bool TryCaptureFields(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue issue,
        out Settlement issueHideout,
        out int randomForStolenTradeGood,
        out Hero counterOfferHero)
    {
        issueHideout = null;
        randomForStolenTradeGood = 0;
        counterOfferHero = null;
        if (issue == null) return false;

        issueHideout = (Settlement)IssueHideoutField.GetValue(issue);
        randomForStolenTradeGood = (int)RandomForStolenTradeGoodField.GetValue(issue);
        counterOfferHero = issue.CounterOfferHero;

        return issueHideout != null && counterOfferHero != null;
    }

    public GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue ConstructReplicated(
        Hero owner, Settlement issueHideout, int randomForStolenTradeGood)
    {
        // The public (Hero, Settlement) ctor independently re-rolls _randomForStolenTradeGood via
        // MBRandom.RandomInt(0, 4) - force it to the server's authoritative roll. CounterOfferHero is
        // deliberately NOT touched here - see RegisterReplicated below for why it must be forced afterward.
        var issue = new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(owner, issueHideout);

        RandomForStolenTradeGoodField.SetValue(issue, randomForStolenTradeGood);

        return issue;
    }

    public void RegisterReplicated(
        Hero owner, GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue issue, Hero counterOfferHero)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);

            // CreateNewIssue's own bookkeeping just ran this peer's own AfterIssueCreation() override, which
            // independently set CounterOfferHero from THIS peer's own local Notables ordering - force it to
            // the server's authoritative value now that bookkeeping is done (see the type doc comment).
            CounterOfferHeroProperty.SetValue(issue, counterOfferHero);
        }
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(
        Hero owner,
        out int stolenTradeGoodAmount,
        out int stolenTradeGoodPrice,
        out int rewardGold,
        out int counterOfferGold)
    {
        stolenTradeGoodAmount = 0;
        stolenTradeGoodPrice = 0;
        rewardGold = 0;
        counterOfferGold = 0;

        if (owner?.Issue?.IssueQuest is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest quest) return false;

        stolenTradeGoodAmount = (int)StolenTradeGoodAmountField.GetValue(quest);
        stolenTradeGoodPrice = (int)StolenTradeGoodPriceField.GetValue(quest);
        rewardGold = quest.RewardGold;
        counterOfferGold = (int)CounterOfferGoldField.GetValue(quest);
        return true;
    }

    public void MirrorQuestAccepted(
        Hero owner,
        int stolenTradeGoodAmount,
        int stolenTradeGoodPrice,
        int rewardGold,
        int counterOfferGold)
    {
        if (owner?.Issue is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest quest) return;

            StolenTradeGoodAmountField.SetValue(quest, stolenTradeGoodAmount);
            StolenTradeGoodPriceField.SetValue(quest, stolenTradeGoodPrice);
            RewardGoldField.SetValue(quest, rewardGold);
            // The double-read fix: force the authoritative value here instead of trusting whatever this
            // peer's own real ctor call independently re-derived from its own live Town.GetItemPrice read.
            CounterOfferGoldField.SetValue(quest, counterOfferGold);
        }
    }

    public bool TryCaptureAlternativeSolutionFields(Hero owner, out int stolenTradeGoodAmount, out int rewardGold)
    {
        stolenTradeGoodAmount = 0;
        rewardGold = 0;

        if (owner?.Issue is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue issue) return false;

        stolenTradeGoodAmount = issue.StolenTradeGoodAmount;
        rewardGold = issue.RewardGold;
        return true;
    }

    public void MirrorAlternativeAccepted(Hero owner, int stolenTradeGoodAmount, int rewardGold)
    {
        if (owner?.Issue is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue issue || !issue.IsOngoingWithoutQuest)
        {
            // Even if the generic mirror already forced the issue state (see the type doc comment on the
            // double-fire with IssueWithAlternativeSolutionAcceptancePatch), still freeze the payout - it's
            // independent of issue state and idempotent either way.
            if (owner?.Issue is GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue)
            {
                GangLeaderStolenGoodsAlternativeSolutionFreeze.Freeze(owner, stolenTradeGoodAmount, rewardGold);
            }
            return;
        }

        using (new AllowedThread())
        {
            IssueStateField.SetValue(issue, SolvingWithAlternativeSolutionStateValue);
            IsTriedToSolveBeforeProperty.SetValue(issue, true);
            issue.IssueDueTime = CampaignTime.Never;
        }

        GangLeaderStolenGoodsAlternativeSolutionFreeze.Freeze(owner, stolenTradeGoodAmount, rewardGold);
    }
}
