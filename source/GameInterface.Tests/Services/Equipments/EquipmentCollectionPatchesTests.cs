using GameInterface.Services.Equipments.Messages;
using GameInterface.Services.Equipments.Patches;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Equipments;

/// <summary>
/// Equipment slots are a struct array, so vanilla stores them with a typed stelem instead of stelem.ref.
/// </summary>
public class EquipmentCollectionPatchesTests
{
    public static IEnumerable<object[]> SlotWriters() => new[]
    {
        new object[] { AccessTools.Method(typeof(Equipment), "set_Item", new[] { typeof(int), typeof(EquipmentElement) }) },
        new object[] { AccessTools.Constructor(typeof(Equipment), new[] { typeof(Equipment) }) },
    };

    [Theory]
    [MemberData(nameof(SlotWriters))]
    public void SlotWrite_GoesThroughTheSyncIntercept(MethodBase method)
    {
        var targets = (IEnumerable<MethodBase>)AccessTools.Method(typeof(EquipmentCollectionPatches), "TargetMethods").Invoke(null, null)!;
        Assert.Contains(method, targets);

        var transpiler = AccessTools.Method(typeof(EquipmentCollectionPatches), "Transpiler");
        var original = PatchProcessor.GetOriginalInstructions(method).ToList();
        var patched = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { original })!).ToList();

        var intercept = AccessTools.Method(typeof(EquipmentCollectionPatches), nameof(EquipmentCollectionPatches.ArrayAssignIntercept))
            .MakeGenericMethod(typeof(EquipmentElement), typeof(ItemSlotsArrayUpdated));

        Assert.Single(original, IsSlotStore);
        Assert.DoesNotContain(patched, IsSlotStore);
        Assert.Single(patched, instruction => instruction.Calls(intercept));
    }

    private static bool IsSlotStore(CodeInstruction instruction)
        => instruction.opcode == OpCodes.Stelem && instruction.operand as System.Type == typeof(EquipmentElement);
}
