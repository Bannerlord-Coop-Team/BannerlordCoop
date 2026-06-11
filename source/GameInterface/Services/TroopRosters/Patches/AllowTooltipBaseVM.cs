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

    [HarmonyPatch(nameof(TooltipBaseVM.Tick))]
    [HarmonyPostfix]
    private static void PostfixTick()
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
    [HarmonyPostfix]
    private static void PostfixTick()
    {
        AllowedThread.RevokeThisThread();
    }
}