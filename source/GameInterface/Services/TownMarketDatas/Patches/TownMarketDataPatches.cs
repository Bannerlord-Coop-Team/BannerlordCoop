using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TownMarketDatas.Patches;

/// <summary>Prevents received roster updates from recalculating authoritative market data on clients.</summary>
[HarmonyPatch(typeof(TownMarketData))]
internal static class TownMarketDataPatches
{
    [ThreadStatic]
    private static int receivedRosterUpdateDepth;

    internal static IDisposable SuppressReceivedRosterUpdate() => new ReceivedRosterUpdateScope();

    [HarmonyPatch(nameof(TownMarketData.OnTownInventoryUpdated))]
    [HarmonyPrefix]
    private static bool OnTownInventoryUpdatedPrefix() =>
        ModInformation.IsServer || receivedRosterUpdateDepth == 0;

    private sealed class ReceivedRosterUpdateScope : IDisposable
    {
        private bool isDisposed;

        public ReceivedRosterUpdateScope()
        {
            receivedRosterUpdateDepth++;
        }

        public void Dispose()
        {
            if (isDisposed) return;

            receivedRosterUpdateDepth--;
            isDisposed = true;
        }
    }
}
