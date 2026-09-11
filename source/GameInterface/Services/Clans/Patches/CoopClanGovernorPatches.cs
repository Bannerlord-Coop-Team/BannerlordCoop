using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using GameInterface.Services.Heroes.Extensions;
using TaleWorlds.CampaignSystem;
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
            (!hero.IsPlayerHero() && CoopClanPermissions.CanManageHero(hero))).ToList();
    }
}
