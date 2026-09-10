using HarmonyLib;
using SandBox.GauntletUI;
using Common.Messaging;
using SandBox.View.Map;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Information.RundownTooltip;
using TaleWorlds.Library.Information;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal static class ClanManagementVMPatches
{
    [HarmonyPatch(typeof(GauntletClanScreen), nameof(GauntletClanScreen.CreateDataSource))]
    [HarmonyPrefix]
    public static bool CreateDataSourcePrefix(GauntletClanScreen __instance, ref ClanManagementVM __result)
    {
        ContainerProvider.TryResolve<IClanFinance>(out var finance);
        ContainerProvider.TryResolve<IMessageBroker>(out var messages);

        __result = new CoopClanManagementVM(__instance.CloseClanScreen, __instance.ShowHeroOnMap,
            __instance.OpenPartyScreenForNewClanParty, __instance.OpenBannerEditorWithPlayerClan, finance, messages);
        return false;
    }

    [HarmonyPatch(typeof(ClanPartiesVM), nameof(ClanPartiesVM.OnPartySelection))]
    [HarmonyPostfix]
    public static void PartySelectionPostfix()
    {
        if ((ScreenManager.TopScreen as GauntletClanScreen)?._dataSource is CoopClanManagementVM coop)
            coop.RefreshFinanceControls();
    }

    [HarmonyPatch(typeof(MapInfoItemVM), MethodType.Constructor, typeof(string), typeof(TooltipTriggerVM))]
    [HarmonyPrefix]
    public static void MapGoldTooltipPrefix(string itemId, ref TooltipTriggerVM tooltipTrigger)
    {
        if (itemId != "gold") return;
        ContainerProvider.TryResolve<IClanFinance>(out var finance);

        // Choose at hover time so an existing map tooltip follows clan joins and departures.
        ExplainedNumber GetGoldChange(bool details) => finance.IsNonLeaderMember(Hero.MainHero)
            ? finance.CalculateMemberGoldChange(Hero.MainHero)
            : Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(Clan.PlayerClan, true, false, details);

        tooltipTrigger = new TooltipTriggerVM(typeof(ExplainedNumber),
            (Func<ExplainedNumber>)(() => GetGoldChange(false)), (Func<ExplainedNumber>)(() => GetGoldChange(true)),
            CampaignUIHelper._changeStr, CampaignUIHelper._totalStr, RundownTooltipVM.ValueCategorization.LargeIsBetter);
    }

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
        bool canManage = CoopClanPermissions.CanManageClan(__instance._clan);
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
        if (__instance is CoopClanManagementVM coop) coop.RefreshFinanceControls();
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
        return CoopClanPermissions.CanManageClan(__instance._clan);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.ExecuteChangeClanName))]
    [HarmonyPrefix]
    public static bool ExecuteChangeClanNamePrefix(ClanManagementVM __instance)
    {
        return CoopClanPermissions.CanManageClan(__instance._clan);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.OnChangeClanNameDone))]
    [HarmonyPrefix]
    public static bool OnChangeClanNameDonePrefix(ClanManagementVM __instance)
    {
        return CoopClanPermissions.CanManageClan(__instance._clan);
    }

    [HarmonyPatch(typeof(MapScreen), nameof(MapScreen.OpenBannerEditorScreen))]
    [HarmonyPrefix]
    public static bool OpenBannerEditorScreenPrefix()
    {
        return CoopClanPermissions.CanManageClan(Hero.MainHero.Clan);
    }
}
