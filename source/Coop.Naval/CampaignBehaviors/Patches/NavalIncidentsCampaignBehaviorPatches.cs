using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalIncidentsCampaignBehaviour))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalIncidentsCampaignBehaviourPatches
{
    // IncidentsCampaignBehaviourInterface rolls the naval triggers on the server for each player, like the land ones
    [HarmonyPatch(nameof(NavalIncidentsCampaignBehaviour.TryInvokeIncident))]
    [HarmonyPrefix]
    public static bool TryInvokeIncidentPrefix() => false;
}