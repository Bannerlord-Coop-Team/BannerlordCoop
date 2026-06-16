using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Workshops.Messages;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace GameInterface.Services.Workshops.Patches;

[HarmonyPatch(typeof(WorkshopsCampaignBehavior))]
internal class WorkshopsCampaignBehaviorPatches
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

    /// <summary>
    /// Replace static properties to handle any player hero generically
    /// </summary>
    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.OnWorkshopOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnWorkshopOwnerChangedPrefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop, Hero oldOwner)
    {
        var message = new WorkshopOwnerChanged(workshop, oldOwner);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.OnWorkshopTypeChanged))]
    [HarmonyPrefix]
    public static bool OnWorkshopTypeChangedPrefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop)
    {
        if (workshop.Owner.IsPlayerHero())
        {
            __instance.RemoveWorkshopData(workshop);
            __instance.AddNewWorkshopData(workshop);
        }

        return false;
    }
}
