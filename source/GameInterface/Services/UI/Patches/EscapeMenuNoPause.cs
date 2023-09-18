using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;

namespace GameInterface.Services.UI.Patches
{
    [HarmonyPatch(typeof(MapScreen))]
    internal class EscapeMenuNoPause
    {
        [HarmonyPatch("OnEscapeMenuToggled")]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Removes the following call GameStateManager.RegisterActiveStateDisableRequest
            // IL_0027: call      class [TaleWorlds.Core] TaleWorlds.Core.Game[TaleWorlds.Core] TaleWorlds.Core.Game::get_Current()
            // IL_002C: callvirt instance class [TaleWorlds.Core] TaleWorlds.Core.GameStateManager[TaleWorlds.Core] TaleWorlds.Core.Game::get_GameStateManager()
            // IL_0031: ldarg.0
            // IL_0032: callvirt instance void [TaleWorlds.Core] TaleWorlds.Core.GameStateManager::RegisterActiveStateDisableRequest(object)

            // And removes the following call GameStateManager.UnregisterActiveStateDisableRequest
            // IL_0061: call      class [TaleWorlds.Core]TaleWorlds.Core.Game [TaleWorlds.Core]TaleWorlds.Core.Game::get_Current()
            // IL_0066: callvirt instance class [TaleWorlds.Core] TaleWorlds.Core.GameStateManager[TaleWorlds.Core] TaleWorlds.Core.Game::get_GameStateManager()
            // IL_006B: ldarg.0
            // IL_006C: callvirt instance void [TaleWorlds.Core] TaleWorlds.Core.GameStateManager::UnregisterActiveStateDisableRequest(object)


            var instrs = instructions.ToList();

            // Amount of IL commands to call either method
            var callOffset = 4;

            // Amount of stack loading before RegisterActiveStateDisableRequest and
            // UnregisterActiveStateDisableRequest calls
            var preOffset = callOffset - 1;

            var disableMethod = typeof(GameStateManager).GetMethod(nameof(GameStateManager.RegisterActiveStateDisableRequest));

            // Finds the index where disableMethod is called
            int idx = instrs.FindIndex(instr => instr.opcode == OpCodes.Callvirt && instr.operand as MethodInfo == disableMethod);

            if (idx < 0) return instructions;

            instrs.RemoveRange(idx - preOffset, callOffset);

            var enableMethod = typeof(GameStateManager).GetMethod(nameof(GameStateManager.UnregisterActiveStateDisableRequest));

            // Finds the index where enableMethod is called
            idx = instrs.FindIndex(instr => instr.opcode == OpCodes.Callvirt && instr.operand as MethodInfo == enableMethod);

            if (idx < 0) return instructions;

            instrs.RemoveRange(idx - preOffset, callOffset);

            return instrs;
        }
    }
}
