using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
public static class ClanIncomePermissionsPatches
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[]
        {
            nameof(ClanFinanceWorkshopItemVM.ExecuteManageWorkshop),
            nameof(ClanFinanceWorkshopItemVM.OnManageWorkshopDone),
            nameof(ClanFinanceWorkshopItemVM.ExecuteSellWorkshop),
            nameof(ClanFinanceWorkshopItemVM.ExecuteToggleWarehouseUsage),
            nameof(ClanFinanceWorkshopItemVM.OnStoreOutputInWarehousePercentageUpdated)
        }) yield return AccessTools.Method(typeof(ClanFinanceWorkshopItemVM), method);

        yield return AccessTools.Method(typeof(ClanFinanceAlleyItemVM), nameof(ClanFinanceAlleyItemVM.ExecuteManageAlley));
        yield return AccessTools.Method(typeof(ClanFinanceAlleyItemVM), nameof(ClanFinanceAlleyItemVM.OnMemberSelection));
    }

    [HarmonyPrefix]
    public static bool ManageAssetPrefix(ClanFinanceIncomeItemBaseVM __instance)
    {
        var owner = __instance is ClanFinanceWorkshopItemVM workshop ? workshop.Workshop.Owner
            : ((ClanFinanceAlleyItemVM)__instance).Alley.Owner;
        return SharedClanPermissions.CanManageClan(owner?.Clan);
    }
}
