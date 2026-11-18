using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalIncidentsCampaignBehaviour))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalIncidentsCampaignBehaviourPatches
{
    // Co-op rolls incidents on the server, so naval incidents stay off until they're synced
    [HarmonyPatch(nameof(NavalIncidentsCampaignBehaviour.TryInvokeIncident))]
    [HarmonyPrefix]
    public static bool TryInvokeIncidentPrefix() => false;
}