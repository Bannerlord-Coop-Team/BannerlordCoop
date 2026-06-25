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
/// gated on <c>Clan.PlayerClan</c>, but the host has no main hero, so on the server that
/// computes nothing (or NREs). On the server we instead add each player clan's alley income
/// summed across all of that clan's heroes' owned alleys, which is authoritative and replicates
/// via <c>GiveGoldAction</c>. Summing over the clan's heroes (rather than just the leader) keeps
/// it correct when several players share one clan. On clients the vanilla per-main-hero
/// calculation is left intact for the local clan finance display.
/// </summary>
[HarmonyPatch(typeof(DefaultClanFinanceModel))]
internal class AlleyIncomePatch
{
    private static readonly TextObject AlleyIncomeText = new TextObject("{=coop_alley_income}Alleys");

    // The Hero.MainHero based version is null-unsafe and clan-wrong on the host, so skip it there.
    [HarmonyPatch("AddPlayerClanIncomeFromOwnedAlleys")]
    [HarmonyPrefix]
    private static bool AddPlayerClanIncomeFromOwnedAlleysPrefix()
    {
        return ModInformation.IsClient;
    }

    [HarmonyPatch("CalculateClanIncomeInternal")]
    [HarmonyPostfix]
    private static void CalculateClanIncomeInternalPostfix(Clan clan, ref ExplainedNumber goldChange)
    {
        if (ModInformation.IsClient) return;
        if (clan == null || !clan.IsPlayerClan()) return;

        // Sum alley income across every hero in the clan, not just the leader, so it stays correct
        // when several players share one clan. Each alley is owned by exactly one hero in exactly one
        // clan, so no alley is double-counted; for a solo player this reduces to their own alleys.
        int income = 0;
        foreach (Hero hero in clan.Heroes)
        {
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
