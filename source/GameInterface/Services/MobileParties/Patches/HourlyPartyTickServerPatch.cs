using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(CampaignEvents))]
internal class HourlyPartyTickServerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("HourlyTickParty")]
    private static bool HourlyTickPartyPrefix(ref MobileParty mobileParty) => ModInformation.IsServer;

}