using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace GameInterface.Services.Time.Patches
{
    /// <summary>
    /// Enables campaign ticks while in menu states to prevent local pausing and desynchronisation.
    /// </summary>
    [HarmonyPatch(typeof(MapState), "OnMapModeTick")]
    class MapStatePatch
    {
        //  Removes the "base.GameStateManager.ActiveState == this" condition from the guard clause.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instrs = instructions.ToList();

            MethodInfo activeStateGetter = typeof(GameStateManager).GetProperty(nameof(GameStateManager.ActiveState)).GetGetMethod();

            for (int i = 0; i < instrs.Count(); i++)
            {
                var instr = instrs[i];

                if (instr.opcode == OpCodes.Callvirt &&
                   instr.operand as MethodInfo == activeStateGetter &&
                   instrs.Count() > i + 2 &&
                   instrs[i - 2].opcode == OpCodes.Ldarg_0 &&
                   instrs[i + 2].opcode == OpCodes.Bne_Un_S)
                {
                    instrs.RemoveRange(i - 2, 5);
                    break;
                }
            }

            return instrs;
        }
    }
}