using HarmonyLib;
using TaleWorlds.CampaignSystem.Incidents;

namespace GameInterface.Services.UI.Patches
{
    // v1.5 moved incident selection out of IncidentsCampaignBehaviour into the campaign's IncidentManager.
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
