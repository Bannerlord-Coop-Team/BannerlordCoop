using GameInterface;
using GameInterface.Services.Ferries.Interfaces;
using HarmonyLib;
using NavalDLC.View;
using NavalDLC.View.Map.Visuals;
using TaleWorlds.CampaignSystem.Naval;

namespace Coop.Naval.PartyVisuals.Patches;

/// <summary>
/// Every player's ferry is drawn with its own departure village's ship, the same on every client.
/// </summary>
[HarmonyPatch(typeof(NavalMobilePartyVisual))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalFerryVisualPatches
{
    // Vanilla keeps the first hull it picks from the main party's village for good, so a later trip or a client that joined
    // after the first one would draw a different ship
    [HarmonyPatch(nameof(NavalMobilePartyVisual.GetFerryShipHull))]
    [HarmonyPrefix]
    private static void GetFerryShipHullPrefix(NavalMobilePartyVisual __instance)
    {
        if (!ContainerProvider.TryResolve<IPlayerFerryInterface>(out var ferryInterface)) return;
        if (!ferryInterface.TryGetDepartureVillage(__instance.MapEntity.MobileParty, out var village)) return;

        __instance._ferryShipHull = NavalDLCViewHelpers.ShipVisualHelper.GetFerryShipHullForSettlement(village);
    }

    // The ferry is a new Ship on every refresh, whose random value picks its look, so each client drew a different one
    [HarmonyPatch(nameof(NavalMobilePartyVisual.AddShipVisual))]
    [HarmonyPrefix]
    private static void AddShipVisualPrefix(NavalMobilePartyVisual __instance, Ship flagship)
    {
        var party = __instance.MapEntity.MobileParty;
        if (!party.IsInFerryState) return;

        flagship.RandomValue = party.Party.RandomValue;
    }
}
