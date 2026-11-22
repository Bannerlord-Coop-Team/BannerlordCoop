using HarmonyLib;
using SandBox.View.Map;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.GameState.Patches;

/// <summary>Allows concurrent game processes to read the shared module navigation cache.</summary>
[HarmonyPatch]
internal static class NavigationCacheDeserializePatch
{
    private static readonly MethodInfo ExclusiveOpen = AccessTools.Method(
        typeof(File), nameof(File.Open), new[] { typeof(string), typeof(FileMode), typeof(FileAccess) });

    private static readonly MethodInfo SharedOpen = AccessTools.Method(
        typeof(File), nameof(File.Open), new[] { typeof(string), typeof(FileMode), typeof(FileAccess), typeof(FileShare) });

    // Copy the installed decoder to a non-generic stand-in before Harmony patches the caller.
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(NavigationCache<Settlement>), nameof(NavigationCache<Settlement>.Deserialize), new[] { typeof(string) })]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void DeserializeShared(NavigationCache<Settlement> cache, string path)
    {
        // Harmony locates this local transpiler when generating the reverse patch.
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => TranspileReadOpen(instructions);
        _ = (Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>>)Transpiler;
        throw new NotSupportedException("Navigation cache reverse patch was not installed.");
    }

    // Avoid detouring the generic base method on Framework's CLR.
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SettlementPositionScript), "ReadNavigationCacheOnGameLoad", new[] { typeof(string), typeof(MobileParty.NavigationType) })]
    internal static IEnumerable<CodeInstruction> RedirectDeserialize(IEnumerable<CodeInstruction> instructions)
    {
        var source = AccessTools.Method(typeof(NavigationCache<Settlement>), nameof(NavigationCache<Settlement>.Deserialize), new[] { typeof(string) });
        var replacement = AccessTools.Method(typeof(NavigationCacheDeserializePatch), nameof(DeserializeShared));
        var codes = instructions.ToList();
        var matches = codes.Where(code => code.Calls(source)).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException($"Expected one NavigationCache<Settlement>.Deserialize call in SettlementPositionScript.ReadNavigationCacheOnGameLoad, found {matches.Count}.");
        var index = codes.IndexOf(matches[0]);
        codes[index] = new CodeInstruction(matches[0]) { opcode = OpCodes.Call, operand = replacement };
        return codes;
    }

    // Validate the whole match before changing only the reader's sharing argument.
    internal static IEnumerable<CodeInstruction> TranspileReadOpen(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var matches = codes.Select((instruction, index) => (instruction, index))
            .Where(pair => pair.instruction.Calls(ExclusiveOpen)).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException(
                $"Expected one File.Open(string, FileMode, FileAccess) in NavigationCache<Settlement>.Deserialize, found {matches.Count}.");

        var index = matches[0].index;
        if (index < 3 || codes[index].opcode != OpCodes.Call || codes[index - 3].opcode != OpCodes.Ldarg_1 ||
            !codes[index - 2].LoadsConstant((int)FileMode.Open) || !codes[index - 1].LoadsConstant((int)FileAccess.Read))
            throw new InvalidOperationException(
                "Expected File.Open(path, FileMode.Open, FileAccess.Read) in NavigationCache<Settlement>.Deserialize.");

        var originalCall = codes[index];
        var share = new CodeInstruction(OpCodes.Ldc_I4, (int)FileShare.Read);
        share.labels.AddRange(originalCall.labels);
        share.blocks.AddRange(originalCall.blocks.Where(block => block.blockType != ExceptionBlockType.EndExceptionBlock));
        var sharedCall = new CodeInstruction(OpCodes.Call, SharedOpen);
        sharedCall.blocks.AddRange(originalCall.blocks.Where(block => block.blockType == ExceptionBlockType.EndExceptionBlock));
        codes[index] = sharedCall;
        codes.Insert(index, share);
        return codes;
    }
}
