using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.Core;
using GameInterface.Policies;

namespace GameInterface.Services.UI.Patches
{
    [HarmonyPatch(typeof(IncidentsCampaignBehaviour))]
    internal class IncidentDisable
    {
        [HarmonyPatch("InvokeIncident")]
        [HarmonyPrefix]
        public static bool InvokeIncidentPatch(Incident incident)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }
    }
}
