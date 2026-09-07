using HarmonyLib;
using SandBox.GauntletUI;
using SandBox.View.Map;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal static class ClanManagementVMPatches
{
    [HarmonyPatch(typeof(ClanManagementVM), MethodType.Constructor,
        typeof(Action), typeof(Action<Hero>), typeof(Action<Hero>), typeof(Action))]
    [HarmonyPostfix]
    public static void ConstructorPostfix(ClanManagementVM __instance)
    {
        RefreshValuesPostfix(__instance);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.RefreshValues))]
    [HarmonyPostfix]
    public static void RefreshValuesPostfix(ClanManagementVM __instance)
    {
        bool canManage = SharedClanPermissions.CanManageClan(__instance._clan);
        var disabledReason = GameTexts.FindText("str_coop_clan_identity_leader_only");
        __instance.CanChooseBanner = canManage;
        __instance.ClanBannerHint = new HintViewModel(canManage
            ? new TextObject("{=Nkue5MX8}Click to edit your clan's banner") : disabledReason);
        __instance.PlayerCanChangeClanName = canManage &&
            __instance.GetPlayerCanChangeClanNameWithReason(out disabledReason);
        __instance.ChangeClanNameHint = new HintViewModel(disabledReason);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.RefreshDailyValues))]
    [HarmonyPostfix]
    public static void RefreshDailyValuesPostfix(ClanManagementVM __instance)
    {
        // Use clan leader's gold for clan finances
        __instance.CurrentGold = __instance._clan.Leader.Gold;
        __instance.ExpectedGold = __instance.CurrentGold + __instance.DailyChange;
    }

    [HarmonyPatch(typeof(GauntletClanScreen), nameof(GauntletClanScreen.OnFrameTick))]
    [HarmonyPrefix]
    public static bool OnFrameTickPrefix(GauntletClanScreen __instance)
    {
        var vm = __instance._dataSource;
        if (vm == null || vm._clan == Hero.MainHero.Clan) return true;

        // The viewed clan is no longer this client's clan. Close the VM
        vm.ExecuteClose();
        return false;
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.ExecuteOpenBannerEditor))]
    [HarmonyPrefix]
    public static bool ExecuteOpenBannerEditorPrefix(ClanManagementVM __instance)
    {
        return SharedClanPermissions.CanManageClan(__instance._clan);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.ExecuteChangeClanName))]
    [HarmonyPrefix]
    public static bool ExecuteChangeClanNamePrefix(ClanManagementVM __instance)
    {
        return SharedClanPermissions.CanManageClan(__instance._clan);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.OnChangeClanNameDone))]
    [HarmonyPrefix]
    public static bool OnChangeClanNameDonePrefix(ClanManagementVM __instance)
    {
        return SharedClanPermissions.CanManageClan(__instance._clan);
    }

    [HarmonyPatch(typeof(MapScreen), nameof(MapScreen.OpenBannerEditorScreen))]
    [HarmonyPrefix]
    public static bool OpenBannerEditorScreenPrefix()
    {
        return SharedClanPermissions.CanManageClan(Hero.MainHero.Clan);
    }
}
