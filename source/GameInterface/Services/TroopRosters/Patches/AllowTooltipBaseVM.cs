using Common.Util;
using HarmonyLib;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(TooltipBaseVM))]
internal class AllowTooltipBaseVM
{
    [HarmonyPatch(nameof(TooltipBaseVM.Tick))]
    [HarmonyPrefix]
    private static void PrefixTick()
    {
        AllowedThread.AllowThisThread();
    }

    // Finalizers (not postfixes) so the revoke runs even when the original throws;
    // a skipped revoke would leave the thread permanently allowed.
    [HarmonyPatch(nameof(TooltipBaseVM.Tick))]
    [HarmonyFinalizer]
    private static void FinalizerTick()
    {
        AllowedThread.RevokeThisThread();
    }
}

[HarmonyPatch(typeof(TooltipProperty))]
internal class AllowTooltipProperty
{
    [HarmonyPatch(nameof(TooltipProperty.RefreshDefinition))]
    [HarmonyPrefix]
    private static void PrefixTick()
    {
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch(nameof(TooltipProperty.RefreshDefinition))]
    [HarmonyFinalizer]
    private static void FinalizerTick()
    {
        AllowedThread.RevokeThisThread();
    }
}