using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.Models.Patches;

[HarmonyPatch(typeof(NavalDLCShipDistributionModel))]
internal class NavalDLCShipDistributionModelPatches
{
    [HarmonyPatch(nameof(NavalDLCShipDistributionModel.CanSendShipToParty))]
    [HarmonyPostfix]
    public static void CanSendShipToPartyPostfix(ref bool __result, MobileParty mobileParty)
    {
        __result = __result && !mobileParty.IsPlayerParty();
    }
}
