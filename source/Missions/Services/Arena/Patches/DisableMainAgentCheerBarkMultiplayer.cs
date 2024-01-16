using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD;

namespace Missions.Services.Agents.Patches
{
    /// <summary>
    /// GameNetwork.IsMultiplayer breaks the constructor for <see cref="MissionMainAgentCheerBarkControllerVM"/>
    /// This patch fixes the check for that
    /// </summary>
    [HarmonyPatch(typeof(MissionMainAgentCheerBarkControllerVM))]
    public class DisableMainAgentCheerBarkMultiplayer
    {
        [HarmonyPatch(MethodType.Constructor, new Type[] { typeof(Action<int>), typeof(Action<int>) })]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instrs = instructions.ToList();

            // replaces "if (GameNetwork.IsMultiplayer)"
            // with "if (false)"
            instrs[11] = new CodeInstruction(OpCodes.Ldc_I4_0);

            return instrs;
        }
    }
}
