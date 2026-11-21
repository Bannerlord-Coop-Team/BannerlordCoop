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
    // The copy constructor fills _itemSlots directly, after the registry has already sent the create
    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(Equipment), searchForStatic: false)
        .Cast<MethodBase>()
        .Concat(AccessTools.GetDeclaredMethods(typeof(Equipment)));

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => ArrayFieldChangeTranspiler<EquipmentElement, ItemSlotsArrayUpdated>(instructions, nameof(Equipment._itemSlots));
}
