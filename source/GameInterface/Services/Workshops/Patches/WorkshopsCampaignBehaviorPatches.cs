using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Workshops.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
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

    // Replace static properties to handle any player hero generically
    // Also has calls to manage warehouse rosters in the CoopSession and updating for clients
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

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.ProduceAnOutputToWarehouse))]
    [HarmonyPrefix]
    public static bool ProduceAnOutputToWarehousePrefix(ref WorkshopsCampaignBehavior __instance, EquipmentElement outputItem, Workshop workshop)
    {
        if (ModInformation.IsClient && CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new OutputProducedToWarehouse(workshop, outputItem);
        MessageBroker.Instance.Publish(__instance, message);

        // Can't manage data in server's warehouse roster. Need to manage with CoopSession for all players
        return false;
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.ConsumeInputFromWarehouse))]
    [HarmonyPrefix]
    public static bool ConsumeInputFromWarehousePrefix(ref WorkshopsCampaignBehavior __instance, ItemCategory productionInput, int inputCount, Workshop workshop)
    {
        var message = new InputConsumedFromWarehouse(workshop, productionInput, inputCount);
        MessageBroker.Instance.Publish(__instance, message);

        // Can't manage data in server's warehouse roster. Need to manage with CoopSession for all players
        return false;
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.TickOneProductionCycleForPlayerWorkshop))]
    [HarmonyPrefix]
    public static bool TickOneProductionCycleForPlayerWorkshopPrefix(ref WorkshopsCampaignBehavior __instance, ref bool __result, WorkshopType.Production production, Workshop workshop, bool effectCapital)
    {
        // Entirely manage this function in WorkshopsCampaignBehaviorInterface instead, called from RunTownWorkshopInternal
        __result = false;
        return false;
    }

    // Possibly replace with transpiler for replacing the one line Hero.MainHero with IsPlayerHero()
    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RunTownWorkshop))]
    [HarmonyPrefix]
    public static bool RunTownWorkshopPrefix(ref WorkshopsCampaignBehavior __instance, Town townComponent, Workshop workshop)
    {
        var message = new TownWorkshopRun(townComponent, workshop);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}