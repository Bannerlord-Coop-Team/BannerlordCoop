using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;

namespace GameInterface.Services.Barters.Patches;

/// <summary>
/// Holds a defection barter at the price the server authorized it at. The client cannot reproduce every
/// input of that valuation, so without this its barter bar and auto-balance show a price the server refuses.
/// </summary>
[HarmonyPatch]
internal static class LordBarterValuationPatch
{
    private static readonly ConditionalWeakTable<JoinKingdomAsClanBarterable, StrongBox<int>> pinnedValues =
        new ConditionalWeakTable<JoinKingdomAsClanBarterable, StrongBox<int>>();

    private static WeakReference<BarterVM> lastBarterVM;

    internal static void Pin(JoinKingdomAsClanBarterable barterable, int value)
    {
        if (barterable == null) return;

        pinnedValues.Remove(barterable);
        pinnedValues.Add(barterable, new StrongBox<int>(value));
    }

    // The screen is built inside BeginPlayerBarter, before the server's price arrives, so redraw it.
    internal static void RefreshBarterScreen(BarterData barter)
    {
        if (lastBarterVM == null || !lastBarterVM.TryGetTarget(out var barterVM)) return;
        if (barterVM._barterData != barter) return;

        barterVM.SendOffer();
    }

    [HarmonyPatch(typeof(JoinKingdomAsClanBarterable), nameof(JoinKingdomAsClanBarterable.GetUnitValueForFaction))]
    [HarmonyPostfix]
    private static void GetUnitValueForFactionPostfix(
        JoinKingdomAsClanBarterable __instance, IFaction factionForEvaluation, ref int __result)
    {
        if (factionForEvaluation != __instance.OriginalOwner?.Clan) return;
        if (!pinnedValues.TryGetValue(__instance, out var pinned)) return;

        __result = pinned.Value;
    }

    [HarmonyPatch(typeof(BarterVM), MethodType.Constructor, typeof(BarterData))]
    [HarmonyPostfix]
    private static void BarterVMConstructorPostfix(BarterVM __instance)
    {
        lastBarterVM = new WeakReference<BarterVM>(__instance);
    }
}
