using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(SeaDamageCampaignBehavior))]
internal class SeaDamageCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(SeaDamageCampaignBehavior.HourlyTickParty))]
    [HarmonyPrefix]
    public static bool HourlyTickPartyPrefix() => ModInformation.IsServer;
}
