using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Equipments.Messages.Events;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
/*
[HarmonyPatch(typeof(Equipment))]
internal class EquipmentFieldPatches
{
    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(Equipment));
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UpdateClanSettlementAutoRecruitment(IEnumerable<CodeInstruction> instructions)
    {
        var itemSlots = AccessTools.Field(typeof(Equipment), nameof(Equipment._itemSlots));
        foreach (var instruction in instructions)
        {
            // When storing the field _itemSlots
            if (instruction.opcode == OpCodes.Stfld &&
                instruction.operand as FieldInfo == itemSlots)
            {
                yield return instruction;
                // This adds a call after when _itemSlots field is set for all methods in the Equipment class
                yield return new CodeInstruction(OpCodes.Ldarg_0); // loads the Equipment instance onto the stack
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EquipmentFieldPatches), nameof(ItemSlotsCreatedIntercept)));

            }
            else
            {
                yield return instruction;
            }
        }
    }
    public static void ItemSlotsCreatedIntercept(Equipment instance)
    {
        // Publish a message here (should only contain the instance)
        // Handle in handler
        // Send to all clients
        // Set _itemSlots to "new EquipmentElement[12]" manually on the network handler

        MessageBroker.Instance.Publish(instance, new ItemSlotsCreated(instance));
    }
}
*/