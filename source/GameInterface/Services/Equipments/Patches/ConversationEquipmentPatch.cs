using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Patches;

[HarmonyPatch(typeof(MapConversationTableau), "SpawnOpponentLeader")]
internal class ConversationEquipmentPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var clone = AccessTools.Method(typeof(Equipment), nameof(Equipment.Clone));
        var displayClone = AccessTools.Method(typeof(ConversationEquipmentPatch), nameof(CloneForDisplay));
        bool replaced = false;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(clone) || instruction.Calls(displayClone))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = displayClone;
                replaced = true;
            }
            yield return instruction;
        }
        if (!replaced)
            throw new InvalidOperationException("Could not find the conversation equipment copy");
    }

    internal static Equipment CloneForDisplay(Equipment equipment, bool cloneWithoutWeapons)
    {
        // Only the display copy is transient; registered hero equipment keeps its normal synchronization.
        using (ModInformation.IsClient ? new TransientEquipmentSyncScope() : null)
            return equipment.Clone(cloneWithoutWeapons);
    }
}
