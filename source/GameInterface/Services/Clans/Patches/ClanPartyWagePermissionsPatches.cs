using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanFinanceExpenseItemVM))]
internal static class ClanPartyWagePermissionsPatches
{
    [HarmonyPatch(nameof(ClanFinanceExpenseItemVM.RefreshValues))]
    [HarmonyPostfix]
    public static void RefreshValuesPostfix(ClanFinanceExpenseItemVM __instance)
    {
        if (ModInformation.IsServer || SharedClanPermissions.CanManageParty(__instance._mobileParty)) return;

        __instance.IsEnabled = false;
        __instance.WageLimitHint.HintText = GameTexts.FindText(__instance._mobileParty?.IsPlayerParty() == true
            ? "str_coop_clan_player_party_wages_disabled" : "str_coop_clan_party_leader_only");
    }

    [HarmonyPatch(nameof(ClanFinanceExpenseItemVM.CurrentWageLimit), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool WageLimitPrefix(ClanFinanceExpenseItemVM __instance) => CanChangeWages(__instance);

    [HarmonyPatch(nameof(ClanFinanceExpenseItemVM.IsUnlimitedWage), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool UnlimitedWagePrefix(ClanFinanceExpenseItemVM __instance) => CanChangeWages(__instance);

    private static bool CanChangeWages(ClanFinanceExpenseItemVM instance)
    {
        if (ModInformation.IsServer || instance.WageLimitHint == null) return true;
        return instance.IsEnabled && SharedClanPermissions.CanManageParty(instance._mobileParty);
    }
}
