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

namespace GameInterface.Services.Time.Patches;

[HarmonyPatch(typeof(GameMenu))]
internal static class DisableGameMenuPausePatches
{
    
    [HarmonyPatch(nameof(GameMenu.ActivateGameMenu))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ActivateGameMenuPatch(IEnumerable<CodeInstruction> instructions) => ReplaceTimeControlMode(instructions);
    
    [HarmonyPatch(nameof(GameMenu.StartWait))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> StartWaitPatch(IEnumerable<CodeInstruction> instructions) => ReplaceTimeControlMode(instructions);

    [HarmonyPatch(nameof(GameMenu.EndWait))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> EndWaitPatch(IEnumerable<CodeInstruction> instructions) => ReplaceTimeControlMode(instructions);

    [HarmonyPatch(nameof(GameMenu.ExitToLast))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ExitToLastPatch(IEnumerable<CodeInstruction> instructions) => ReplaceTimeControlMode(instructions);

    [HarmonyPatch(nameof(GameMenu.SwitchToMenu))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SwitchToMenuPatch(IEnumerable<CodeInstruction> instructions) => ReplaceTimeControlMode(instructions);

    private static IEnumerable<CodeInstruction> ReplaceTimeControlMode(IEnumerable<CodeInstruction> instructions)
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