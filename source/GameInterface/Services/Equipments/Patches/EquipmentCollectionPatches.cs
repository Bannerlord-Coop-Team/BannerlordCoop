using GameInterface.Services.Equipments.Messages;
using GameInterface.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Patches;

[HarmonyPatch]
internal class EquipmentCollectionPatches : GenericPatches<EquipmentCollectionPatches, Equipment>
{
    new static IEnumerable<MethodBase> TargetMethods() => GenericPatches<EquipmentCollectionPatches, Equipment>.TargetMethods();

    [HarmonyPrepare]
    private static bool Prepare() => TargetMethods().Any();
    
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => ArrayFieldChangeTranspiler<EquipmentElement, ItemSlotsArrayUpdated>(instructions, nameof(Equipment._itemSlots));
}
