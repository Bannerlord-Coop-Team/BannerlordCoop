using Common.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the access <see cref="Patches.ArtisanOverpricedGoodsIssueCreationPatch"/>/
/// <see cref="Handlers.ArtisanOverpricedGoodsIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue"/>.
///
/// CREATION: the Issue ctor takes <c>(Hero issueOwner, Hero counterOfferHero, ItemObject requestedTradeGood)</c>
/// DIRECTLY - both the antagonist-merchant pick (<c>GetAntagonistMerchant</c>'s real
/// <c>Notables.GetRandomElementWithPredicate(...)</c> roll, a genuine per-client-divergent pick if independently
/// re-rolled) and the requested trade good (deterministic given synced Town price-index data, but still an
/// object reference resolved from a fixed-order scan - captured for the same "don't trust independent
/// re-derivation" reason every other bespoke interface in this family does) are picked SERVER-SIDE inside
/// <c>ArtisanOverpricedGoodsIssueBehavior.ConditionsHold</c>, before <c>IssueManager.CreateNewIssue</c> is ever
/// called (see <see cref="Patches.IssueManagerCreateNewIssuePatches"/> - creation is already fully
/// server-authoritative). So, exactly like <see cref="SmugglersIssueInterface"/>, <see cref="ConstructReplicated"/>
/// needs NO reflection at all - the real public ctor accepts both captured values directly, and
/// <c>CounterOfferHero</c> is a real public (<c>protected set</c>) <see cref="IssueBase"/> override, so
/// <see cref="TryCaptureFields"/> needs no reflection either. <c>CalculateTradeGoodsAmountAndReward()</c> (run
/// unconditionally inside the ctor) deterministically derives <c>RequestedTradeGoodAmount</c>/<c>_goldReward</c>
/// from <c>_requestedTradeGood.Value</c> and <c>base.IssueDifficultyMultiplier</c> alone - the same
/// deterministic, shared-<c>Campaign.Current.PlayerProgress</c>-derived value <see cref="SmugglersIssueInterface"/>'s
/// doc comment already establishes is safe to let every peer's own ctor call independently recompute (confirmed
/// against the decompiled <c>DefaultIssueModel.GetIssueDifficultyMultiplier</c>) - so once
/// <c>_requestedTradeGood</c>/<c>CounterOfferHero</c> are captured/forced, <c>RequestedTradeGoodAmount</c>/
/// <c>_goldReward</c> land on the SAME value on every peer without needing their own force-write.
/// <see cref="Handlers.ArtisanOverpricedGoodsIssueHandler"/> still defensively logs if either ever lands at
/// exactly 0 right after construction - <c>MathF.Max(..., 1)</c> makes <c>RequestedTradeGoodAmount</c> == 0
/// structurally impossible, but nothing equivalently floors <c>_goldReward</c>, and the Issue's own
/// <c>OnGameLoad</c> silently RE-DERIVES both (live, from whatever this peer's own <c>IssueDifficultyMultiplier</c>
/// is at load time) the moment either is ever exactly 0 - a divergence risk worth catching loudly rather than
/// letting it silently recompute differently across peers on a save/load.
///
/// ACCEPT TIME: <c>GenerateIssueQuest</c> forwards <c>IssueOwner</c>/a fixed 30-day duration/<c>_requestedTradeGood</c>/
/// <c>RewardGold</c>/<c>RequestedTradeGoodAmount</c>/<c>CounterOfferHero</c> - ALL already frozen/deterministic by
/// the time creation is captured (see above), so - contrary to this task's initial assumption, verified against
/// the decompiled source rather than trusted - a bare replay of <c>IssueManager.StartIssueQuest</c> on every
/// peer lands on a byte-identical <c>ArtisanOverpricedGoodsIssueQuest</c>: this type IS
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/> (and, since
/// <c>IsThereAlternativeSolution == true</c> with <c>AlternativeSolutionScaleFlags == Duration</c> only,
/// confirmed against the decompiled source - no <c>FailureRisk</c>/<c>Casualties</c> -
/// <see cref="GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible"/> too), needing NO bespoke
/// accept-time capture/force-write of its own for these scalar fields at all - same shape as Smugglers/Artisan
/// Can't Sell.
///
/// THE ACTUAL BESPOKE MECHANIC this type needs (confirmed against the decompiled source, a real
/// vanilla-adjacent bug independent of the above): <c>ArtisanOverpricedGoodsIssueQuest</c>'s ctor RECEIVES
/// <c>counterOfferHero</c> as a parameter but never assigns it to any field - dead input. Every access point
/// (<c>IsSuitableToTalk</c>, <c>AcceptCounterOffer</c>, <c>OnCompleteWithSuccess</c>) instead re-derives a
/// private <c>AntagonistHero</c> property LIVE via <c>base.QuestGiver.CurrentSettlement.Notables.FirstOrDefault(x
/// =&gt; x != base.QuestGiver &amp;&amp; x.IsMerchant &amp;&amp; x.GetTraitLevel(DefaultTraits.Mercy) &lt;= 0)</c>
/// - order-dependent (not the Issue's own random pick) AND missing the <c>CanHaveCampaignIssues()</c> filter
/// <c>GetAntagonistMerchant</c> applies at Issue-creation time - so it can silently resolve to a DIFFERENT hero
/// than the one actually offered, or to null (<c>AcceptCounterOffer</c> has zero null-guard:
/// <c>antagonistHero.AddPower(5f)</c> is a guaranteed NRE if no notable currently matches the weaker predicate).
/// Fixed via <see cref="Patches.ArtisanOverpricedGoodsAntagonistIdentityPatches"/> - a Harmony getter-redirect
/// on <c>AntagonistHero</c> itself, freezing the ctor's own <c>counterOfferHero</c> parameter (captured by a
/// ctor Postfix, keyed by <c>QuestGiver</c>) the same "intercept a property getter" shape
/// <c>GangLeaderNeedsToOffloadStolenGoodsPriceFreezePatches</c> already established - see that patch's own doc
/// comment for the precedent. Since the frozen value comes from the ctor parameter (itself already
/// captured/forced identical on every peer at Issue-creation time, well before the Quest is ever built), this
/// fix needs NO network message of its own - it runs identically, locally, on every peer the instant its own
/// real Quest ctor call happens (genuine accept OR mirror replay).
/// </summary>
public interface IArtisanOverpricedGoodsIssueInterface : IGameAbstraction
{
    /// <summary>Reads the two server-picked fields off an already-constructed issue (direct field/property
    /// reads - see the type doc comment on why no reflection is needed here). Returns false if either is
    /// missing, which should never happen for a real instance.</summary>
    bool TryCaptureFields(
        ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue issue,
        out Hero counterOfferHero,
        out ItemObject requestedTradeGood);

    /// <summary>
    /// Builds a <see cref="ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue"/> via its real
    /// public ctor, which takes both captured values directly - no field-forcing/reflection needed (see the
    /// type doc comment). Does not register it with the <see cref="IssueManager"/> - see
    /// <see cref="RegisterReplicated"/>.
    /// </summary>
    ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue ConstructReplicated(
        Hero owner, Hero counterOfferHero, ItemObject requestedTradeGood);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead
    /// of constructing (and re-rolling the antagonist merchant/trade good on) a new one - same technique as
    /// <see cref="VillageNeedsToolsIssueInterface.RegisterReplicated"/>.
    /// </summary>
    void RegisterReplicated(Hero owner, ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue issue);
}

/// <inheritdoc cref="IArtisanOverpricedGoodsIssueInterface"/>
public class ArtisanOverpricedGoodsIssueInterface : IArtisanOverpricedGoodsIssueInterface
{
    public bool TryCaptureFields(
        ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue issue,
        out Hero counterOfferHero,
        out ItemObject requestedTradeGood)
    {
        counterOfferHero = null;
        requestedTradeGood = null;
        if (issue == null) return false;

        counterOfferHero = issue.CounterOfferHero;
        requestedTradeGood = issue._requestedTradeGood;

        return counterOfferHero != null && requestedTradeGood != null;
    }

    public ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue ConstructReplicated(
        Hero owner, Hero counterOfferHero, ItemObject requestedTradeGood)
    {
        return new ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue(owner, counterOfferHero, requestedTradeGood);
    }

    public void RegisterReplicated(Hero owner, ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
