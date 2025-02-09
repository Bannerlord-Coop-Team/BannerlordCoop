using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using Common.Logging;
using Serilog;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages.Collections;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Services.TroopRosters.Messages;

namespace GameInterface.Services.TroopRosters.Patches
{
    [HarmonyPatch]
    internal class TroopRosterCollectionPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterCollectionPatches>();

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var dataStack = new Stack<CodeInstruction>();
            CodeInstruction previous = null;
            var TroopRosterDataArray = AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster.data));
            var arrayAssignIntercept = AccessTools.Method(typeof(TroopRosterCollectionPatches), nameof(ArrayAssignIntercept));

            foreach (var instruction in instructions)
            {
                // Track Hero load instructions before accessing VolunteerTypes
                if (instruction.opcode == OpCodes.Ldfld && (FieldInfo)instruction.operand == TroopRosterDataArray)
                {
                    if (previous != null && IsLdloc(previous))
                    {
                        dataStack.Push(previous);
                    }
                    else
                    {
                        dataStack.Push(null);
                    }
                }

                // Replace `stelem.ref` with intercept call
                if (instruction.opcode == OpCodes.Stelem_Ref)
                {
                    if (dataStack.Count > 0)
                    {
                        var rosterLoad = dataStack.Pop();
                        if (rosterLoad != null)
                        {
                            yield return rosterLoad; // Inject Hero instance
                            yield return new CodeInstruction(OpCodes.Call, arrayAssignIntercept) { labels = instruction.labels };
                            continue;
                        }
                    }
                }

                yield return instruction;
                previous = instruction; // Track previous instruction
            }
        }

        // Helper method to check if an instruction loads a local variable
        private static bool IsLdloc(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Ldloc || instruction.opcode == OpCodes.Ldloc_S ||
                   instruction.opcode == OpCodes.Ldloc_0 || instruction.opcode == OpCodes.Ldloc_1 ||
                   instruction.opcode == OpCodes.Ldloc_2 || instruction.opcode == OpCodes.Ldloc_3;
        }

        public static void ArrayAssignIntercept(TroopRosterElement[] TroopRosterElements, int index, TroopRosterElement value, TroopRoster instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                TroopRosterElements[index] = value;
                return;
            }

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(TroopRosterElement), Environment.StackTrace);
                return;
            }
            var message = new TroopRosterDataUpdated(instance, value, index);
            MessageBroker.Instance.Publish(instance, message);

            TroopRosterElements[index] = value;
        }
    }
}
