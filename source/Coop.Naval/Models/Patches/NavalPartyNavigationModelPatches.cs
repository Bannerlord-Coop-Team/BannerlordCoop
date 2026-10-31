using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.Models.Patches;

[HarmonyPatch(typeof(NavalPartyNavigationModel))]
internal class NavalPartyNavigationModelPatches
{
    [HarmonyPatch(nameof(NavalPartyNavigationModel.HasNavalNavigationCapability))]
    [HarmonyPostfix]
    public static void HasNavalNavigationCapabilityPostfix(ref bool __result, MobileParty mobileParty)
    {
        // Replace MobileParty.IsMainParty usage so a player party without ships doesn't use its army's ships
        __result = __result && (mobileParty.Ships.Count > 0 || !mobileParty.IsPlayerParty());
    }
}
