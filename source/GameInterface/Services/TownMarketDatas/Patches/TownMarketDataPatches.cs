using Common;
using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TownMarketDatas.Patches;

/// <summary>Prevents received roster updates from recalculating authoritative market data on clients.</summary>
[HarmonyPatch(typeof(TownMarketData))]
internal static class TownMarketDataPatches
{
    [HarmonyPatch(nameof(TownMarketData.OnTownInventoryUpdated))]
    [HarmonyPrefix]
    private static bool OnTownInventoryUpdatedPrefix() =>
        ModInformation.IsServer || !AllowedThread.IsThisThreadAllowed();
}
