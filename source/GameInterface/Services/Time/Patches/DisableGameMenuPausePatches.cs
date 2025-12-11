using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using SandBox;
using SandBox.View.Map;

namespace GameInterface.Services.Time.Patches;

[HarmonyPatch]
internal static class DisableGameMenuPausePatches
{
    [HarmonyPrepare]
    static bool Prepare()
    {
        return AccessTools.Method(typeof(MapScreen), "HandleLeftMouseButtonClick") != null;
    }

    static IEnumerable<MethodBase> TargetMethods()
    {
        var m = AccessTools.Method(typeof(MapScreen), "HandleLeftMouseButtonClick");
        return m != null ? new MethodBase[] { m } : System.Array.Empty<MethodBase>();
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> instrs = instructions.ToList();

        MethodInfo timeControlSetter = typeof(Campaign).GetProperty(nameof(Campaign.TimeControlMode)).GetSetMethod();
        MethodInfo deferFunction = typeof(DisableMapClickTimeChange).GetMethod("SetTimeControlModeDeference", BindingFlags.Static | BindingFlags.NonPublic);

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
