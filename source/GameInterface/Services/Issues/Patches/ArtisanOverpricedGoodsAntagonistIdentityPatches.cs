using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Per-QuestGiver freeze of the correct antagonist merchant Hero, populated once by
/// <see cref="ArtisanOverpricedGoodsAntagonistIdentityPatches"/>'s ctor Postfix (the same instant EVERY peer's
/// own real <c>ArtisanOverpricedGoodsIssueQuest</c> ctor call happens - genuine accept or mirror replay, see
/// <see cref="Interfaces.IArtisanOverpricedGoodsIssueInterface"/>'s doc comment for why no network message is
/// needed here at all), and consulted by the getter-redirect Prefix below instead of letting
/// <c>AntagonistHero</c> re-derive itself live. Keyed by <c>QuestGiver</c> - the SAME <see cref="Hero"/>
/// reference the Quest object itself was built with and continues to use for the rest of its lifetime,
/// deliberately NOT by <c>QuestGiver.Issue</c>/<c>IssueOwner</c> (see the type doc comment on
/// <see cref="ArtisanOverpricedGoodsAntagonistIdentityPatches"/> for why that distinction matters: this key
/// survives a genuinely reachable <c>IssueManager.ChangeIssueOwner</c> transfer - e.g. the artisan issue-owner
/// notable dying mid-quest, see <c>NotablesCampaignBehavior</c>'s dead-notable-replacement handling - unharmed,
/// since that call only ever touches <c>Hero.Issue</c>/<c>IssueBase.IssueOwner</c>, never this Quest's own
/// already-snapshotted <c>QuestGiver</c> field).
/// </summary>
internal static class ArtisanOverpricedGoodsAntagonistFreeze
{
    private static readonly Dictionary<Hero, Hero> AntagonistByQuestGiver = new();

    public static void Freeze(Hero questGiver, Hero antagonistHero)
    {
        if (questGiver == null || antagonistHero == null) return;
        AntagonistByQuestGiver[questGiver] = antagonistHero;
    }

    public static bool TryGet(Hero questGiver, out Hero antagonistHero)
    {
        antagonistHero = null;
        return questGiver != null && AntagonistByQuestGiver.TryGetValue(questGiver, out antagonistHero);
    }

    /// <summary>Drained on finalize (see <see cref="ArtisanOverpricedGoodsAntagonistFreezeCleanupPatch"/>) so a
    /// stale entry never leaks into this hero's NEXT, unrelated issue - same leak concern
    /// <c>GangLeaderStolenGoodsAlternativeSolutionFreeze</c>'s own doc comment documents.</summary>
    public static void Clear(Hero questGiver)
    {
        if (questGiver == null) return;
        AntagonistByQuestGiver.Remove(questGiver);
    }
}

/// <summary>
/// The identity-bug fix (see <see cref="Interfaces.IArtisanOverpricedGoodsIssueInterface"/>'s type doc comment
/// for the full derivation): <c>ArtisanOverpricedGoodsIssueQuest</c>'s ctor receives <c>counterOfferHero</c> as
/// a parameter but never assigns it to any field - every access point instead re-derives a private
/// <c>AntagonistHero</c> property LIVE via an order-dependent <c>Notables.FirstOrDefault(...)</c> that's also
/// missing the <c>CanHaveCampaignIssues()</c> filter the Issue's own <c>GetAntagonistMerchant</c> applies at
/// creation time - so it can silently resolve to a DIFFERENT hero than the one actually offered, or to null
/// (<c>AcceptCounterOffer</c>'s <c>antagonistHero.AddPower(5f)</c> has zero null-guard).
///
/// Fixed the same "intercept a property getter" way <c>GangLeaderNeedsToOffloadStolenGoodsPriceFreezePatches</c>
/// already established as this codebase's precedent for redirecting a getter's LOGIC (not just its backing
/// field) to a captured/frozen value: <see cref="AntagonistHeroPrefix"/> below consults
/// <see cref="ArtisanOverpricedGoodsAntagonistFreeze"/> (populated by <see cref="QuestConstructorPostfix"/> the
/// instant any real Quest ctor call happens, on every peer, genuine or mirrored) instead of letting the real
/// getter run.
///
/// Task item 4 (<c>IssueManager.ChangeIssueOwner</c> reachability): confirmed reachable - decompiled
/// <c>NotablesCampaignBehavior</c> calls it when a notable dies and is replaced
/// (<c>if (deadNotable.Issue != null) Campaign.Current.IssueManager.ChangeIssueOwner(deadNotable.Issue, newNotable);</c>),
/// and this issue type's owner (the artisan) IS itself a notable who can die mid-quest. That call only ever
/// mutates <c>Hero.Issue</c>/<c>IssueBase.IssueOwner</c> - it never touches the already-built Quest object's own
/// <c>QuestGiver</c> field. Since <see cref="ArtisanOverpricedGoodsAntagonistFreeze"/> is keyed by
/// <c>QuestGiver</c> (not <c>QuestGiver.Issue</c>/<c>IssueOwner</c>), a mid-quest ownership transfer cannot ever
/// invalidate the freeze lookup - so <see cref="AntagonistHeroPrefix"/> never needs (and deliberately does not
/// have) a "QuestGiver.Issue went null, silently fall back" branch. The ONLY fallback path is "no freeze entry
/// was ever recorded for this QuestGiver at all", which should be structurally impossible for any quest built
/// after this fix (the ctor Postfix always runs first) - if it ever happens anyway (e.g. an old save from
/// before this fix shipped), this fails LOUDLY (a logged Error, not a crash) rather than silently falling back
/// to the buggy live re-derivation with no trace.
/// </summary>
[HarmonyPatch]
internal class ArtisanOverpricedGoodsAntagonistIdentityPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArtisanOverpricedGoodsAntagonistIdentityPatches>();

    [HarmonyPatch(
        typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest),
        MethodType.Constructor,
        new[] { typeof(string), typeof(Hero), typeof(CampaignTime), typeof(ItemObject), typeof(int), typeof(int), typeof(Hero) })]
    [HarmonyPostfix]
    private static void QuestConstructorPostfix(Hero questGiver, Hero counterOfferHero)
    {
        ArtisanOverpricedGoodsAntagonistFreeze.Freeze(questGiver, counterOfferHero);
    }

    [HarmonyPatch(typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest), "AntagonistHero", MethodType.Getter)]
    [HarmonyPrefix]
    private static bool AntagonistHeroPrefix(
        ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest __instance, ref Hero __result)
    {
        if (ArtisanOverpricedGoodsAntagonistFreeze.TryGet(__instance.QuestGiver, out var frozen))
        {
            __result = frozen;
            return false;
        }

        Logger.Error(
            "No frozen antagonist Hero found for ArtisanOverpricedGoodsIssueQuest.QuestGiver {QuestGiver} - " +
            "falling back to the buggy live re-derivation (Notables.FirstOrDefault, missing the " +
            "CanHaveCampaignIssues() filter GetAntagonistMerchant applies at creation time). This should never " +
            "happen for a quest built after this fix; likely cause: a save created before this fix shipped.",
            __instance.QuestGiver);
        return true;
    }
}

/// <summary>
/// Drains <see cref="ArtisanOverpricedGoodsAntagonistFreeze"/> on every genuine or mirrored
/// <c>IssueFinalized</c> for this issue type, so a stale frozen antagonist never leaks into this hero's NEXT,
/// unrelated issue - same leak concern <c>GangLeaderNeedsToOffloadStolenGoodsFinalizeCleanupPatch</c>'s own doc
/// comment documents. Deliberately drains via the QUEST's own <c>QuestGiver</c> (falling back to
/// <c>IssueOwner</c> only if there's no quest object at all, e.g. the issue was cancelled before ever being
/// accepted, in which case the freeze was never populated to begin with) rather than <c>IssueOwner</c> alone -
/// see <see cref="ArtisanOverpricedGoodsAntagonistFreeze"/>'s own doc comment on why the two can genuinely
/// diverge after a mid-quest <c>IssueManager.ChangeIssueOwner</c> transfer.
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class ArtisanOverpricedGoodsAntagonistFreezeCleanupPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (__instance is not ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue issue) return;

        var questGiver = (issue.IssueQuest as ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest)?.QuestGiver
            ?? issue.IssueOwner;

        ArtisanOverpricedGoodsAntagonistFreeze.Clear(questGiver);
    }
}
