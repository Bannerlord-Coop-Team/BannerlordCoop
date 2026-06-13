using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Workshops.Patches;

[HarmonyPatch(typeof(WorkshopsCampaignBehavior))]
internal class DisableWorkshopsCampaignBehavior
{
    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    static bool RegisterEventsPrefix(ref WorkshopsCampaignBehavior __instance)
    {
        // Only want to allow this event on the client
        // OnAfterSessionLaunched initialises game menu options related to managing workshops
        if (ModInformation.IsClient)
        {
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(__instance, new Action<CampaignGameStarter>(__instance.OnAfterSessionLaunched));
            return false;
        }

        return true;
    }
}
