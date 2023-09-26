using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(MapScreen))]
internal class DisableMapClickTimeChagne
{
    private static void SetTimeControlModeDeference(Campaign _, CampaignTimeControlMode _2)
    {
        ;
    }

    [HarmonyPatch("HandleLeftMouseButtonClick")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> instrs = instructions.ToList();

        MethodInfo timeControlSetter = typeof(Campaign).GetProperty(nameof(Campaign.TimeControlMode)).GetSetMethod();
        MethodInfo deferFunction = typeof(DisableMapClickTimeChagne).GetMethod("SetTimeControlModeDeference", BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var instr in instructions)
        {
            if (instr.opcode == OpCodes.Callvirt &&
                instr.operand as MethodInfo == timeControlSetter)
            {
                instr.opcode = OpCodes.Call;
                instr.operand = deferFunction;
            }
        }

        return instrs;
    }

    
}


