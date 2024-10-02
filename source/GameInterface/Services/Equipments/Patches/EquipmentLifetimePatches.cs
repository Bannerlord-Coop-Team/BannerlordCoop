using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;
using GameInterface.Services.Equipments.Messages.Events;


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
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);

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
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);


            return true;
        }

            MessageBroker.Instance.Publish(__instance, new EquipmentWithParamCreated(__instance, equipment));

        return true;
    } 

}
