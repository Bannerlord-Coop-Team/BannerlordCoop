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
    private static readonly MethodInfo MapState_OnTick = typeof(MapState).GetMethod("OnTick", BindingFlags.NonPublic | BindingFlags.Instance);

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

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.RegisterActiveStateDisableRequest))]
    [HarmonyPrefix]
    static bool RegisterActiveStateDisableRequestPatch()
    {
        return false;
    }


    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.OnTick))]
    [HarmonyPrefix]
    static void OnTickPatch(ref GameStateManager __instance, float dt)
    {
        if (!(__instance.ActiveState is MapState))
        {
            MapState mapState = __instance.LastOrDefault<MapState>();
            if (mapState == null) return;

            MapState_OnTick.Invoke(mapState, new object[] { dt });
        }
    }

    // Remove "base.GameStateManager.ActiveState == this" condition from if statement
    [HarmonyPatch(typeof(MapState), "OnMapModeTick")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OnMapModeTickPatch(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> instrs = instructions.ToList();

        MethodInfo activeStateGetter = typeof(GameStateManager).GetProperty(nameof(GameStateManager.ActiveState)).GetGetMethod();

        for (int i = 0; i < instrs.Count(); i++)
        {
            var instr = instrs[i];

            if (instr.opcode == OpCodes.Callvirt &&
               instr.operand as MethodInfo == activeStateGetter &&
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
