using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.MapEvents;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MapEvents.Messages;
using TaleWorlds.Core;
using GameInterface.Services.Equipments.Messages.Events;

namespace GameInterface.Services.Equipments.Patches;

[HarmonyPatch]
internal class EquipmentCollectionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<EquipmentCollectionPatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(Equipment));

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var stack = new Stack<CodeInstruction>();

        var itemSlotArrayType = AccessTools.Field(typeof(Equipment), nameof(Equipment._itemSlots));
        var arrayAssignIntercept = AccessTools.Method(typeof(EquipmentCollectionPatches), nameof(ArrayAssignIntercept));
        foreach (var instruction in instructions)
        {
            if (stack.Count > 0 && instruction.opcode == OpCodes.Stelem_Ref)
            {
                stack.Pop();

                var newInstr = new CodeInstruction(OpCodes.Call, arrayAssignIntercept);
                newInstr.labels = instruction.labels;

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return newInstr;
                continue;
            }

            if (instruction.opcode == OpCodes.Ldfld && instruction.operand as FieldInfo == itemSlotArrayType)
            {
                stack.Push(instruction);
            }

            yield return instruction;
        }
    }

    public static void ArrayAssignIntercept(EquipmentElement[] _itemSlots, int index, EquipmentElement value, Equipment instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            _itemSlots[index] = value;
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
            return;
        }

        var message = new ItemSlotsArrayUpdated(instance, value.Item, value.ItemModifier, index);
        MessageBroker.Instance.Publish(instance, message);

        _itemSlots[index] = value;
    }
}
