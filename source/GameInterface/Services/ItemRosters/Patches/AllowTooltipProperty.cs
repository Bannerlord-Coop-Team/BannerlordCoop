using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.ItemRosters.Patches;

[HarmonyPatch(typeof(PropertyBasedTooltipVM))]
internal class AllowTooltipProperty
{
    [HarmonyPatch("OnPeriodicRefresh")]
    [HarmonyPrefix]
    private static void PrefixRefreshValue()
    {
        AllowedThread.AllowThisThread();
    }

    // Finalizers (not postfixes) so the revoke runs even when the original throws;
    // a skipped revoke would leave the thread permanently allowed.
    [HarmonyPatch("OnPeriodicRefresh")]
    [HarmonyFinalizer]
    private static void FinalizerRefreshValue()
    {
        AllowedThread.RevokeThisThread();
    }
}

[HarmonyPatch(typeof(GauntletInformationView))]
internal class AllowInformationView
{
    [HarmonyPatch("OnShowTooltip")]
    [HarmonyPrefix]
    private static void PrefixRefreshValue()
    {
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch("OnShowTooltip")]
    [HarmonyFinalizer]
    private static void FinalizerRefreshValue()
    {
        AllowedThread.RevokeThisThread();
    }
}

[HarmonyPatch(typeof(TooltipRefresherCollection))]
internal class AllowTooltipRefresher
{
    [HarmonyPatch("RefreshSettlementTooltip")]
    [HarmonyPrefix]
    private static void PrefixRefreshValue()
    {
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch("RefreshSettlementTooltip")]
    [HarmonyFinalizer]
    private static void FinalizerRefreshValue()
    {
        AllowedThread.RevokeThisThread();
    }
}