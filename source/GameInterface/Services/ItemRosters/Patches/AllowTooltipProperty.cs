using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.ItemRosters.Patches;

[HarmonyPatch(typeof(TooltipProperty))]
internal class AllowTooltipProperty
{
    [HarmonyPatch(nameof(TooltipProperty.RefreshValue))]
    [HarmonyPrefix]
    private static void PrefixRefreshValue()
    {
        AllowedThread.AllowThisThread();
    }

    // Finalizers (not postfixes) so the revoke runs even when the original throws;
    // a skipped revoke would leave the thread permanently allowed.
    [HarmonyPatch(nameof(TooltipProperty.RefreshValue))]
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