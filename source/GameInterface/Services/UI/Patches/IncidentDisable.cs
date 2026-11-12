using HarmonyLib;
using TaleWorlds.CampaignSystem.Incidents;

namespace GameInterface.Services.UI.Patches
{
    // Incident selection lives in the campaign's IncidentManager.
    [HarmonyPatch(typeof(IncidentManager))]
    internal class IncidentDisable
    {
        [HarmonyPatch("InvokeIncident")]
        [HarmonyPrefix]
        public static bool InvokeIncidentPatch(Incident incident)
        {
            return false;
        }
    }
}
