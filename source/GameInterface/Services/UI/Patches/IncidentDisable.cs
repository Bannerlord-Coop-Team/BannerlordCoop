using HarmonyLib;
using TaleWorlds.CampaignSystem.Incidents;

namespace GameInterface.Services.UI.Patches
{
    // Co-op rolls incidents on the server and sets MapState.NextIncident itself, so this only blocks NavalIncidentsCampaignBehaviour's local rolls
    [HarmonyPatch(typeof(IncidentManager))]
    internal class IncidentDisable
    {
        [HarmonyPatch(nameof(IncidentManager.TryInvokeIncident))]
        [HarmonyPrefix]
        public static bool TryInvokeIncidentPatch()
        {
            return false;
        }
    }
}
