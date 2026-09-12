using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.Save.Patches;

/// <summary>
/// Repairs duplicate faction-stance keys created when an imported faction's campaign id changed.
/// </summary>
[HarmonyPatch(typeof(ContainerLoadData), nameof(ContainerLoadData.FillObject))]
internal static class ContainerLoadDataPatches
{
    private static readonly MethodInfo DictionaryAddMethod = AccessTools.Method(
        typeof(IDictionary),
        nameof(IDictionary.Add),
        new[] { typeof(object), typeof(object) });

    private static readonly MethodInfo AddDictionaryEntryMethod = AccessTools.Method(
        typeof(ContainerLoadDataPatches),
        nameof(AddDictionaryEntry));

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var insertionCallCount = 0;

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(DictionaryAddMethod))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AddDictionaryEntryMethod;
                insertionCallCount++;
            }
            else if (instruction.Calls(AddDictionaryEntryMethod))
                insertionCallCount++;

            yield return instruction;
        }

        if (insertionCallCount != 1)
            throw new InvalidOperationException(
                $"Expected one dictionary insertion call in {nameof(ContainerLoadData)}.{nameof(ContainerLoadData.FillObject)}, found {insertionCallCount}.");
    }

    internal static void AddDictionaryEntry(IDictionary dictionary, object key, object value)
    {
        if (dictionary is Dictionary<(IFaction, IFaction), StanceLink> && dictionary.Contains(key))
        {
            dictionary[key] = value;
            return;
        }

        dictionary.Add(key, value);
    }
}
