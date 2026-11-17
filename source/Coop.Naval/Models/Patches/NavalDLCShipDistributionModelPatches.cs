using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.Models.Patches;

[HarmonyPatch(typeof(NavalDLCShipDistributionModel))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalDLCShipDistributionModelPatches
{
    [HarmonyPatch(nameof(NavalDLCShipDistributionModel.CanSendShipToParty))]
    [HarmonyPostfix]
    public static void CanSendShipToPartyPostfix(ref bool __result, Ship ship, MobileParty mobileParty)
    {
        // Like vanilla's MainParty check, a player only gets ships another player sends, not ships the clan's AI hands out
        __result = __result && (!mobileParty.IsPlayerParty() || ship.Owner.IsPlayerParty());
    }
}
