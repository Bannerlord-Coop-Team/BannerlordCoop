﻿using Common.Logging;
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

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class MapEventCollectionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventCollectionPatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(MapEvent));

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var stack = new Stack<CodeInstruction>();

        var sidesArrayType = AccessTools.Field(typeof(MapEvent), nameof(MapEvent._sides));
        var arrayAssignIntercept = AccessTools.Method(typeof(MapEventCollectionPatches), nameof(ArrayAssignIntercept));
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

            if (instruction.opcode == OpCodes.Ldfld && instruction.operand as FieldInfo == sidesArrayType)
            {
                stack.Push(instruction);
            }

            yield return instruction;
        }
    }

    public static void ArrayAssignIntercept(MapEventSide[] _sides, int index, MapEventSide value, MapEvent instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            _sides[index] = value;
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
            return;
        }

        var message = new MapEventSidesArrayUpdated(instance, value, index);
        MessageBroker.Instance.Publish(instance, message);

        _sides[index] = value;
    }
}
