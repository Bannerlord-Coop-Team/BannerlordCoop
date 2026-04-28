using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.Core;

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
