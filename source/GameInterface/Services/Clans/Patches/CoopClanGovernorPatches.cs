using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using GameInterface.Services.Heroes.Extensions;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal class CoopClanGovernorPatches
{
    [HarmonyPatch(typeof(ClanFiefsVM), nameof(ClanFiefsVM.GetCanChangeGovernor))]
    [HarmonyPrefix]
    public static bool GetCanChangeGovernorPrefix(ref bool __result, ref TextObject disabledReason)
    {
        if (CoopClanPermissions.CanManageClan(Hero.MainHero.Clan)) return true;

        __result = false;
        disabledReason = GameTexts.FindText("str_coop_clan_governor_leader_only");
        return false;
    }

    [HarmonyPatch(typeof(TownManagementVM), nameof(TownManagementVM.GetCanChangeGovernor))]
    [HarmonyPrefix]
    public static bool TownGetCanChangeGovernorPrefix(ref bool __result, ref TextObject disabledReason)
        => GetCanChangeGovernorPrefix(ref __result, ref disabledReason);

    [HarmonyPatch(typeof(ClanFiefsVM), nameof(ClanFiefsVM.GetGovernorCandidates))]
    [HarmonyPostfix]
    public static void GetGovernorCandidatesPostfix(ref IEnumerable<ClanCardSelectionItemInfo> __result)
    {
        __result = __result.Where(candidate => !(candidate.Identifier is Hero hero) ||
            (hero.IsPlayerHero()
                ? hero.Clan == Hero.MainHero.Clan && hero != hero.Clan.Leader
                : CoopClanPermissions.CanManageHero(hero))).ToList();
    }

    [HarmonyPatch(typeof(FactionHelper), nameof(FactionHelper.IsMainClanMemberAvailableForSendingSettlementAsGovernor))]
    [HarmonyPrefix]
    public static bool IsAvailableAsGovernorPrefix(Hero hero, Settlement settlementOfGovernor,
        ref bool __result, ref TextObject explanation)
    {
        if (!hero.IsPlayerHero()) return true;

        __result = CoopClanPermissions.CanManageClan(hero.Clan) && hero.GovernorOf == null &&
            settlementOfGovernor == null && Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(hero);
        explanation = __result ? null : GameTexts.FindText("str_coop_clan_player_governor_unavailable");
        return false;
    }

    [HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetTeleportationDelayText))]
    [HarmonyPrefix]
    public static bool GetTeleportationDelayTextPrefix(Hero hero, PartyBase target, ref TextObject __result)
    {
        if (hero == null || !hero.IsPlayerHero() || target?.Settlement?.OwnerClan != hero.Clan) return true;

        __result = GameTexts.FindText("str_coop_clan_player_governor_effects");
        return false;
    }

    [HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetGovernorSelectionConfirmationPopupTexts))]
    [HarmonyPrefix]
    public static bool GetGovernorConfirmationTextsPrefix(Hero newGovernor, Settlement settlement,
        ref (TextObject titleText, TextObject bodyText) __result)
    {
        if (newGovernor == null || !newGovernor.IsPlayerHero() || settlement == null) return true;

        var body = GameTexts.FindText("str_coop_clan_assign_player_governor");
        body.SetTextVariable("HERO", newGovernor.Name);
        body.SetTextVariable("SETTLEMENT", settlement.Name);
        __result = (GameTexts.FindText("str_clan_assign_governor"), body);
        return false;
    }
}
