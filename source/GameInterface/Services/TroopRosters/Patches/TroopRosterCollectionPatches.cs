using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches
{
    [HarmonyPatch]
    internal class TroopRosterCollectionPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterCollectionPatches>();

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var arrayAssignMethod = AccessTools.Method(typeof(TroopRosterCollectionPatches), nameof(ArrayAssignIntercept));
            var troopRosterElementType = typeof(TroopRosterElement);
            var dataField = AccessTools.Field(typeof(TroopRoster), "data");

            var matcher = new CodeMatcher(instructions)
                .MatchStartForward( // Find all stelem.any instructions for TroopRosterElement
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(ci => ci.opcode == OpCodes.Ldfld && ci.operand == dataField),
                    new CodeMatch(ci => ci.opcode == OpCodes.Stelem_Ref)
                )
                .Repeat(match => // Process each match
                {
                    match.Advance(-1); // Move to stelem.ref instruction

                    // Replace stelem.ref with our interceptor
                    match.SetInstruction(
                        new CodeInstruction(OpCodes.Call, arrayAssignMethod)
                    );

                    // Ensure stack balance by re-adding arguments
                    match.Insert(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, dataField),
                        new CodeInstruction(OpCodes.Ldloc_1), // Replace with actual index local
                        new CodeInstruction(OpCodes.Ldloc_2)  // Replace with actual value local
                    );
                });

            return matcher.InstructionEnumeration();
        }

        // Intercept method with proper parameters
        public static void ArrayAssignIntercept(
            TroopRosterElement[] array,
            int index,
            TroopRosterElement value)
        {
            // Custom logic here (e.g., validation, logging)
            array[index] = value; // Preserve original assignment
        }
    }
}