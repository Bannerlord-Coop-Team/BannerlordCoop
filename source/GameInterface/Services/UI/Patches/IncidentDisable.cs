using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Incidents;

namespace GameInterface.Services.UI.Patches
{
    [HarmonyPatch(typeof(IncidentsCampaignBehaviour))]
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
