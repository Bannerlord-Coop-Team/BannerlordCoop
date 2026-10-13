using HarmonyLib;
using NavalDLC.View.Map.Managers;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.PartyVisuals.Patches;

/// <summary>
/// The naval manager only adds a party's ship visual on MobilePartyCreated, which clients don't fire for parties created by the server.
/// </summary>
[HarmonyPatch(typeof(MobilePartyVisualManager))]
internal class NavalPartyVisualLifetimePatches
{
    [HarmonyPatch(nameof(MobilePartyVisualManager.AddNewPartyVisualForParty))]
    [HarmonyPostfix]
    private static void AddNewPartyVisualForPartyPostfix(MobileParty mobileParty)
    {
        NavalMobilePartyVisualManager.Current?.AddNewPartyVisualForParty(mobileParty);
    }

    [HarmonyPatch(nameof(MobilePartyVisualManager.RemovePartyVisualForParty))]
    [HarmonyPostfix]
    private static void RemovePartyVisualForPartyPostfix(MobileParty mobileParty)
    {
        NavalMobilePartyVisualManager.Current?.RemovePartyVisualForParty(mobileParty);
    }
}
