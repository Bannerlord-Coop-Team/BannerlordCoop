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
    static IEnumerable<MethodBase> TargetMethods()
    {
        return new MethodBase[]
        {
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.StartWait)),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.EndWait)),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)),
            AccessTools.Method(typeof(MapScreen), "HandleLeftMouseButtonClick"),
            AccessTools.Method(typeof(MapScreen), "HandleMouse"),
        };
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