using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;

namespace GameInterface.Services.Time.Patches;

[HarmonyPatch]
internal static class DisableGameMenuPausePatches
{
    // Swapped in for the TimeControlMode setter so the write is dropped
    private static void DropTimeControlModeWrite(Campaign _, CampaignTimeControlMode _2)
    {
    }

    static IEnumerable<MethodBase> TargetMethods()
    {
        return new MethodBase[]
        {
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.StartWait)),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.EndWait)),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)),
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)),
        };
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> instrs = instructions.ToList();

        MethodInfo timeControlSetter = typeof(Campaign).GetProperty(nameof(Campaign.TimeControlMode)).GetSetMethod();
        MethodInfo dropWrite = typeof(DisableGameMenuPausePatches).GetMethod(nameof(DropTimeControlModeWrite), BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var instr in instructions)
        {
            if (instr.opcode == OpCodes.Callvirt &&
                instr.operand as MethodInfo == timeControlSetter)
            {
                instr.opcode = OpCodes.Call;
                instr.operand = dropWrite;
            }
        }

        return instrs;
    }
}