using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.ArtisanCantSellProductsAtAFairPriceIssueCreationPatch"/>/
/// <see cref="Handlers.ArtisanCantSellProductsAtAFairPriceIssueHandler"/> need to capture and authoritatively
/// replicate a <see cref="ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue"/>.
///
/// FOUR creation-time-rolled fields (one more than Deliver the Herd's three chained rolls), all set directly in
/// the Issue's own ctor (see the decompiled source) - none chained to each other, so order doesn't matter for
/// forcing them, unlike Deliver the Herd:
/// <c>_targetSettlement</c> (<c>SelectTargetSettlement</c> - depends on <c>MobileParty.MainParty</c>'s own
/// position, genuinely per-client-divergent even though the search itself is deterministic given that input),
/// <c>_targetHero</c> (<c>_targetSettlement.Notables.GetRandomElementWithPredicate(...)</c> - a real
/// <c>MBRandom</c> roll, and also dependent on which settlement was picked), <c>_rawMaterialsToBeDelivered</c>
/// (<c>_possibleDeliveryItems.GetRandomElement()</c> - a real roll among 7 candidate items), and
/// <c>CounterOfferHero</c> (<c>SelectCounterOfferHero</c> - a <c>FirstOrDefault</c> over the settlement's
/// Notables; not an <c>MBRandom</c> roll, but still an object reference resolved from local Notables-list
/// ordering rather than anything guaranteed identical across peers, so it is captured/forced for safety exactly
/// like Deliver the Herd's chained <c>_targetHero</c> pick).
///
/// Unlike Deliver the Herd, <c>AfterIssueCreation()</c> is NOT overridden here - all four fields are set
/// directly in the constructor body - so forcing them immediately after construction (before
/// <see cref="RegisterReplicated"/> ever calls <c>IssueManager.CreateNewIssue</c>) is safe and final; nothing
/// downstream re-derives them. Contrast this with Gang Leader Needs to Offload Stolen Goods, whose
/// <c>CounterOfferHero</c> IS set via an <c>AfterIssueCreation()</c> override that
/// <c>IssueManager.CreateNewIssue</c>'s own bookkeeping calls AFTER the factory returns - forcing that field
/// before <c>RegisterReplicated</c> there would get silently overwritten; see
/// <see cref="IGangLeaderNeedsToOffloadStolenGoodsIssueInterface"/>'s doc comment for how that case is handled
/// instead.
///
/// GenerateIssueQuest reads only these four forced fields plus <c>RawMaterialCountToBeDelivered</c>/
/// <c>RewardGold</c> (both pure functions of <c>IssueDifficultyMultiplier</c>, never an ambient roll) and the
/// constant 18-day quest duration - so once creation is captured/forced, the quest-solution accept path rides
/// the fully generic mirror (see <see cref="GenericAcceptMirrorIssueTypes"/>) unchanged, same as Deliver the
/// Herd. <c>IsThereAlternativeSolution == true</c> with <c>AlternativeSolutionScaleFlags == Duration</c> only
/// (confirmed against the decompiled source - no <c>FailureRisk</c>/<c>Casualties</c>), so the alternative-
/// solution path also needs no additional per-client-divergent field beyond what the shared
/// <c>MirrorAlternativeAccepted</c> already forces.
///
/// This type ALSO has <c>IsThereLordSolution == true</c> - the same unbuilt-infrastructure gap
/// <see cref="ILadysKnightOutIssueInterface"/>'s doc comment documents (no <c>IssueBase.StartIssueWithLordSolution</c>
/// sync mechanism exists anywhere in this project) - disabled the same way, by forcing
/// <c>IsThereLordSolution</c> to always report false (see
/// <see cref="Patches.ArtisanCantSellProductsAtAFairPriceLordSolutionDisablePatch"/>). The Quest-level "ambush
/// counter-offer" dialogue (<c>GetCounterOfferDialogFlow</c>, registered from <c>QuestAcceptedConsequences</c>
/// AND <c>InitializeQuestOnGameLoad</c> - the same dual-registration-site shape Deliver the Herd's own second
/// dialogue has) is a SEPARATE, quest-solution-path-only mechanic unrelated to the Lord Solution's own built-in
/// (and here-disabled) counter-offer flow - see
/// <see cref="Patches.ArtisanCantSellProductsAtAFairPriceOwnershipGatePatches"/>'s doc comment for why it needs
/// its own ownership gate regardless.
/// </summary>
public interface IArtisanCantSellProductsAtAFairPriceIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue,
        out Settlement targetSettlement,
        out Hero targetHero,
        out ItemObject rawMaterialsToBeDelivered,
        out Hero counterOfferHero);

    ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue ConstructReplicated(
        Hero owner, Settlement targetSettlement, Hero targetHero, ItemObject rawMaterialsToBeDelivered, Hero counterOfferHero);

    void RegisterReplicated(Hero owner, ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue);
}

/// <inheritdoc cref="IArtisanCantSellProductsAtAFairPriceIssueInterface"/>
public class ArtisanCantSellProductsAtAFairPriceIssueInterface : IArtisanCantSellProductsAtAFairPriceIssueInterface
{
    private static readonly FieldInfo TargetSettlementField =
        AccessTools.Field(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue), "_targetSettlement");
    private static readonly FieldInfo TargetHeroField =
        AccessTools.Field(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue), "_targetHero");
    private static readonly FieldInfo RawMaterialsToBeDeliveredField =
        AccessTools.Field(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue), "_rawMaterialsToBeDelivered");
    private static readonly PropertyInfo CounterOfferHeroProperty =
        AccessTools.Property(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue), nameof(IssueBase.CounterOfferHero));

    public bool TryCaptureFields(
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue,
        out Settlement targetSettlement,
        out Hero targetHero,
        out ItemObject rawMaterialsToBeDelivered,
        out Hero counterOfferHero)
    {
        targetSettlement = null;
        targetHero = null;
        rawMaterialsToBeDelivered = null;
        counterOfferHero = null;
        if (issue == null) return false;

        targetSettlement = (Settlement)TargetSettlementField.GetValue(issue);
        targetHero = (Hero)TargetHeroField.GetValue(issue);
        rawMaterialsToBeDelivered = (ItemObject)RawMaterialsToBeDeliveredField.GetValue(issue);
        counterOfferHero = issue.CounterOfferHero;

        return targetSettlement != null && targetHero != null && rawMaterialsToBeDelivered != null && counterOfferHero != null;
    }

    public ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue ConstructReplicated(
        Hero owner, Settlement targetSettlement, Hero targetHero, ItemObject rawMaterialsToBeDelivered, Hero counterOfferHero)
    {
        // The public (Hero) ctor is the only way to build one, and it independently rolls all four fields
        // (SelectTargetSettlement, then GetRandomElementWithPredicate off that settlement's Notables,
        // GetRandomElement over the 7 possible delivery items, and SelectCounterOfferHero). None of the four
        // is set via AfterIssueCreation here (unlike Gang Leader's CounterOfferHero - see the type doc
        // comment), so forcing all four immediately, before RegisterReplicated ever runs CreateNewIssue's own
        // bookkeeping, is safe and final.
        var issue = new ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue(owner);

        TargetSettlementField.SetValue(issue, targetSettlement);
        TargetHeroField.SetValue(issue, targetHero);
        RawMaterialsToBeDeliveredField.SetValue(issue, rawMaterialsToBeDelivered);
        CounterOfferHeroProperty.SetValue(issue, counterOfferHero);

        return issue;
    }

    public void RegisterReplicated(Hero owner, ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
