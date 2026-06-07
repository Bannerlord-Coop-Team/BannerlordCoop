using Common.Util;
using HarmonyLib;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using GameInterface.Policies;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(TooltipBaseVM))]
internal class AllowTooltipBaseVM
{
    [HarmonyPatch(nameof(TooltipBaseVM.Tick))]
    [HarmonyPrefix]
    private static void PrefixTick()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch(nameof(TooltipBaseVM.Tick))]
    [HarmonyPostfix]
    private static void PostfixTick()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
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
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch(nameof(TooltipProperty.RefreshDefinition))]
    [HarmonyPostfix]
    private static void PostfixTick()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        AllowedThread.RevokeThisThread();
    }
}