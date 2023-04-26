using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time.Patches
{
    [HarmonyPatch(typeof(MapScreen))]
    internal class MapClickPausePatch
    {
        private static void SetTimeControlModeDeference(Campaign _, CampaignTimeControlMode _2)
        {
            ;
        }

        [HarmonyPatch("HandleLeftMouseButtonClick")]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List< CodeInstruction> instrs = instructions.ToList();

            MethodInfo timeControlSetter = typeof(Campaign).GetProperty(nameof(Campaign.TimeControlMode)).GetSetMethod();
            MethodInfo deferFunction = typeof(MapClickPausePatch).GetMethod("SetTimeControlModeDeference", BindingFlags.Static | BindingFlags.NonPublic);

            foreach(var instr in instructions)
            {
                if(instr.opcode == OpCodes.Callvirt &&
                    instr.operand == timeControlSetter)
                {
                    instr.opcode = OpCodes.Call;
                    instr.operand = deferFunction;
                }
            }

            return instrs;
        }
    }
}
