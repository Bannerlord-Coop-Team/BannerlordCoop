using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Workshops.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

    // Possibly replace with transpiler for replacing the one line Hero.MainHero with IsPlayerHero()
    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RunTownWorkshop))]
    [HarmonyPrefix]
    public static bool RunTownWorkshopPrefix(ref WorkshopsCampaignBehavior __instance, Town townComponent, Workshop workshop)
    {
        WorkshopType workshopType = workshop.WorkshopType;
        bool flag = false;
        for (int i = 0; i < workshopType.Productions.Count; i++)
        {
            float num = workshop.GetProductionProgress(i);
            if (num > 1f)
            {
                num = 1f;
            }
            num += Campaign.Current.Models.WorkshopModel.GetEffectiveConversionSpeedOfProduction(workshop, workshopType.Productions[i].ConversionSpeed, false).ResultNumber;
            if (num >= 1f)
            {
                bool flag2 = true;
                while (flag2 && num >= 1f)
                {
                    WorkshopType.Production production = workshopType.Productions[i];
                    bool flag3;
                    if (!production.Inputs.Any((ValueTuple<ItemCategory, int> x) => !x.Item1.IsTradeGood))
                    {
                        flag3 = !production.Outputs.Any((ValueTuple<ItemCategory, int> x) => !x.Item1.IsTradeGood);
                    }
                    else
                    {
                        flag3 = false;
                    }
                    bool flag4 = flag3;
                    flag2 = ((workshop.Owner.IsPlayerHero()) ? __instance.TickOneProductionCycleForPlayerWorkshop(production, workshop, flag4) : __instance.TickOneProductionCycleForNotableWorkshop(production, workshop, flag4));
                    if (flag2 && flag4)
                    {
                        flag = true;
                    }
                    num -= 1f;
                }
            }
            workshop.SetProgress(i, num);
        }
        if (flag)
        {
            workshop.UpdateLastRunTime();
        }

        return false;
    }
}