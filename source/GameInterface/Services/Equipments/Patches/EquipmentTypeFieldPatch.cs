using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Equipments.Messages.Events;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;

/*
namespace GameInterface.Services.Equipments.Patches;

[HarmonyPatch]
public class EquipmentTypeFieldPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<EquipmentTypeFieldPatch>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(Equipment));
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> EquipmentTypeTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(Equipment), nameof(Equipment._equipmentType));
        var fieldIntercept = AccessTools.Method(typeof(EquipmentTypeFieldPatch), nameof(EquipmentTypeIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void EquipmentTypeIntercept(Equipment instance, Equipment.EquipmentType newEquipmentType)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._equipmentType = newEquipmentType;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._equipmentType = newEquipmentType;
            return;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            objectManager.TryGetId(instance, out var EquipmentId);

            MessageBroker.Instance.Publish(instance, new EquipmentTypeChanged((int)newEquipmentType, EquipmentId));

            instance._equipmentType = newEquipmentType;
        }
        else
        {
            Logger.Error("ObjectManager not resolved: {callstack}", Environment.StackTrace);
            instance._equipmentType = newEquipmentType;
            return;
        }
    }
}
*/