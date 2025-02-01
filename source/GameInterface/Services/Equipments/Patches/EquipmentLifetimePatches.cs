using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;
using GameInterface.Services.Equipments.Messages.Events;
using GameInterface.Services.Heroes.Messages;
using TaleWorlds.CampaignSystem;


namespace GameInterface.Services.Equipments.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="Equipment"/> objects.
/// </summary>
[HarmonyPatch]
internal class EquipmentLifetimePatches

    
{
    private static readonly ILogger Logger = LogManager.GetLogger<EquipmentLifetimePatches>();

    [HarmonyPatch(typeof(Equipment), MethodType.Constructor)]
    [HarmonyPrefix]
    private static bool CreateEquipmentPrefix(Equipment __instance)
    {

        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        
        if (ModInformation.IsClient)
        {   /*
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
            */
            return true;
        }

        MessageBroker.Instance.Publish(__instance, new EquipmentCreated(__instance));
            
        return true;
    }
    
    [HarmonyPatch(typeof(Equipment), MethodType.Constructor, typeof(Equipment))]
    [HarmonyPrefix]
    private static bool CreateEquipmentParamPrefix(Equipment __instance, Equipment equipment)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
           /* Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);

            */
            return true;
        }

            MessageBroker.Instance.Publish(__instance, new EquipmentCreated(__instance));

        return true;
    }

    [HarmonyPatch(typeof(Hero), nameof(Hero.OnDeath))]
    [HarmonyPrefix]
    private static bool OnDeathPrefix(ref Hero __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Hero), Environment.StackTrace);
            return true;
        }

        var message = new EquipmentRemoved(__instance.BattleEquipment, __instance.CivilianEquipment);

        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }


    [HarmonyPatch(typeof(Hero), nameof(Hero.ResetEquipments))]
    [HarmonyPrefix]
    private static void ResetEquipmentPrefix(ref Hero __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Hero), Environment.StackTrace);
            return;
        }

        var message = new EquipmentRemoved(__instance.BattleEquipment, __instance.CivilianEquipment);

        MessageBroker.Instance.Publish(__instance, message);

    }


}
