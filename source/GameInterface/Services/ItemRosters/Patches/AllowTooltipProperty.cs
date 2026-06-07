using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.MountAndBlade.GauntletUI;
using GameInterface.Policies;

namespace GameInterface.Services.ItemRosters.Patches;

[HarmonyPatch(typeof(TooltipProperty))]
internal class AllowTooltipProperty
{
    [HarmonyPatch(nameof(TooltipProperty.RefreshValue))]
    [HarmonyPrefix]
    private static void PrefixRefreshValue()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch(nameof(TooltipProperty.RefreshValue))]
    [HarmonyPostfix]
    private static void PostfixRefreshValue()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
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
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch("OnShowTooltip")]
    [HarmonyPostfix]
    private static void PostfixRefreshValue()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
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
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch("RefreshSettlementTooltip")]
    [HarmonyPostfix]
    private static void PostfixRefreshValue()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        AllowedThread.RevokeThisThread();
    }
}