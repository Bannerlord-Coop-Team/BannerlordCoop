using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Keeps the mission escape menu on vanilla's multiplayer no-pause path without changing mission behavior globally.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletEscapeMenuBase), "OnEscapeMenuToggled")]
internal static class MissionEscapeMenuNoPausePatch
{
    private static readonly MethodInfo IsMultiplayerGetter = AccessTools.PropertyGetter(
        typeof(GameNetwork),
        nameof(GameNetwork.IsMultiplayer));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UseMultiplayerPauseBehavior(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(IsMultiplayerGetter))
            {
                instruction.opcode = OpCodes.Ldc_I4_1;
                instruction.operand = null;
            }

            yield return instruction;
        }
    }
}
