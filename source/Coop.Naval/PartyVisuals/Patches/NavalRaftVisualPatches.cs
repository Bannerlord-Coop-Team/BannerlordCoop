using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using NavalDLC.View.Map.Visuals;

namespace Coop.Naval.PartyVisuals.Patches;

/// <summary>
/// Vanilla adds the raft model hidden for every party that isn't the main party and never shows it again.
/// Other players' rafts are shown so a player sailing back after escaping captivity can be seen.
/// </summary>
[HarmonyPatch(typeof(NavalMobilePartyVisual))]
internal class NavalRaftVisualPatches
{
    [HarmonyPatch(nameof(NavalMobilePartyVisual.AddRaftVisual))]
    [HarmonyPostfix]
    private static void AddRaftVisualPostfix(NavalMobilePartyVisual __instance)
    {
        var party = __instance.MapEntity.MobileParty;
        if (party == null || party.IsMainParty || !party.IsPlayerParty()) return;

        // Fading in and out with the party's visibility is done on the parent entity, same as a ship
        __instance._shipEntity.SetVisibilityExcludeParents(true);
        __instance._shipEntity.SetAlpha(1f);
    }
}
