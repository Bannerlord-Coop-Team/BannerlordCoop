using Common;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Alleys.Patches;

/// <summary>
/// Re-points alley income so it works in co-op. Vanilla sums <c>Hero.MainHero.OwnedAlleys</c>
/// gated on <c>Clan.PlayerClan</c>, but the host has no main hero. We replace it on BOTH sides with a
/// per-clan sum over all of that clan's heroes' owned alleys (the authoritative gold is computed and
/// applied on the server and replicates via <c>GiveGoldAction</c>; the same calculation runs on the
/// client purely so its clan-finance display matches). Summing over the clan's heroes - rather than
/// just the leader or a single main hero - keeps it correct when several players share one clan.
///
/// This patch is in <see cref="DeferredCategory"/> so the early, pre-load <c>PatchAll</c> does NOT
/// apply it. <c>DefaultClanFinanceModel</c> is <c>beforefieldinit</c> and its static ctor eagerly calls
/// <c>Game.Current.GameTextManager.FindText</c>; touching the type during the pre-load patch pass (when
/// <c>Game.Current</c> is null) throws in that ctor and the CLR caches the failure, killing all clan
/// finance for the run (frozen clan screen + AI ticks aborting). <see cref="AlleyIncomePatchHandler"/>
/// applies this category on <c>GameLoaded</c> instead, once the text manager exists.
/// </summary>
[HarmonyPatch(typeof(DefaultClanFinanceModel))]
[HarmonyPatchCategory(DeferredCategory)]
internal class AlleyIncomePatch
{
    /// <summary>Applied after game load (see the type remarks), not by the early <c>PatchAll</c>.</summary>
    public const string DeferredCategory = "CoopAlleyIncomeDeferred";

    private static readonly TextObject AlleyIncomeText = new TextObject("{=coop_alley_income}Alleys");

    // Vanilla's Hero.MainHero based version is null-unsafe on the host and undercounts a shared clan
    // (only the local main hero's alleys), so skip it everywhere; the postfix below replaces it.
    [HarmonyPatch("AddPlayerClanIncomeFromOwnedAlleys")]
    [HarmonyPrefix]
    private static bool AddPlayerClanIncomeFromOwnedAlleysPrefix()
    {
        return false;
    }

    [HarmonyPatch("CalculateClanIncomeInternal")]
    [HarmonyPostfix]
    private static void CalculateClanIncomeInternalPostfix(Clan clan, ref ExplainedNumber goldChange)
    {
        if (clan == null || !clan.IsPlayerClan()) return;

        // Sum alley income across every living hero in the clan, not just the leader, so it stays
        // correct when several players share one clan. Each alley is owned by exactly one hero in
        // exactly one clan, so no alley is double-counted; for a solo player this reduces to their own
        // alleys. Dead heroes are skipped because clan.Heroes still contains them (the cache isn't
        // pruned on death, only on leaving the clan), so without this a dead owner's alley would keep
        // paying out.
        int income = 0;
        foreach (Hero hero in clan.Heroes)
        {
            if (!hero.IsAlive) continue;

            foreach (Alley alley in hero.OwnedAlleys)
            {
                income += Campaign.Current.Models.AlleyModel.GetDailyIncomeOfAlley(alley);
            }
        }

        if (income != 0)
        {
            goldChange.Add(income, AlleyIncomeText);
        }
    }
}
