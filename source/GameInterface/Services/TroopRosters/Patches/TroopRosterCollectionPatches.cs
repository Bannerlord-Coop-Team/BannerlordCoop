using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Equipments.Messages.Events;
using GameInterface.Services.Equipments.Patches;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.TroopRosters.Patches
{
    [HarmonyPatch]
    internal class TroopRosterCollectionPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterCollectionPatches>();

        static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(TroopRoster));

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var stack = new Stack<CodeInstruction>();

            var dataArrayType = AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster.data));
            var arrayAssignIntercept = AccessTools.Method(typeof(TroopRosterCollectionPatches), nameof(ArrayAssignIntercept));
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

                if (instruction.opcode == OpCodes.Ldfld && instruction.operand as FieldInfo == dataArrayType)
                {
                    stack.Push(instruction);
                }

                yield return instruction;
            }
        }

        public static void ArrayAssignIntercept(TroopRosterElement[] data, int index, TroopRosterElement value, TroopRoster instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                data[index] = value;
                return;
            }

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
                return;
            }

            var message = new TroopRosterDataUpdated(instance, value, index);
            MessageBroker.Instance.Publish(instance, message);

            data[index] = value;
        }
    }
}