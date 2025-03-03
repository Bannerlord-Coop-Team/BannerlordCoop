using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using GameInterface.Services.Equipments.Messages.Events;
using GameInterface.Utils;

namespace GameInterface.Services.Equipments.Patches;

[HarmonyPatch]
internal class EquipmentCollectionPatches : GenericPatches<EquipmentCollectionPatches, Equipment>
{
    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(Equipment));
    
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => ArrayChangeTranspiler<EquipmentElement, ItemSlotsArrayUpdated>(instructions, nameof(Equipment._itemSlots));
}
