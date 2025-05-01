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
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        // Equiptment is cloned on the client for party icon
        if (config.IsClient) return true;

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, new EquipmentCreated(__instance));
            
        return true;
    }
    
    [HarmonyPatch(typeof(Equipment), MethodType.Constructor, typeof(Equipment))]
    [HarmonyPrefix]
    private static bool CreateEquipmentParamPrefix(Equipment __instance, Equipment equipment)
    {
        // Call original if we call this function
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, new EquipmentCreated(__instance));

        return true;
    }

    [HarmonyPatch(typeof(Hero), nameof(Hero.OnDeath))]
    [HarmonyPrefix]
    private static void OnDeathPrefix(ref Hero __instance)
    {
        if (CallPolicy.IsOriginalAllowed()) return;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        var message = new EquipmentRemoved(__instance.BattleEquipment, __instance.CivilianEquipment);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, message);
    }


    [HarmonyPatch(typeof(Hero), nameof(Hero.ResetEquipments))]
    [HarmonyPrefix]
    private static void ResetEquipmentPrefix(ref Hero __instance)
    {
        if (CallPolicy.IsOriginalAllowed()) return;

        if (CallPolicy.SkipIfClient(Logger, out var _)) return;

        var message = new EquipmentRemoved(__instance.BattleEquipment, __instance.CivilianEquipment);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, message);

    }


}
