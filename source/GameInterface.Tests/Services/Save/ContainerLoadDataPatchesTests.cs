using GameInterface.Services.Save.Patches;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem.Load;
using Xunit;

namespace GameInterface.Tests.Services.Save;

/// <summary>
/// Covers the narrow faction-stance migration applied while TaleWorlds fills saved dictionaries.
/// </summary>
public class ContainerLoadDataPatchesTests
{
    [Fact]
    public void AddDictionaryEntry_DuplicateStanceKey_KeepsLaterSavedValue()
    {
        var firstFaction = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        var secondFaction = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        var key = ((IFaction)firstFaction, (IFaction)secondFaction);
        var firstStance = (StanceLink)FormatterServices.GetUninitializedObject(typeof(StanceLink));
        var laterStance = (StanceLink)FormatterServices.GetUninitializedObject(typeof(StanceLink));
        IDictionary dictionary = new Dictionary<(IFaction, IFaction), StanceLink>();

        ContainerLoadDataPatches.AddDictionaryEntry(dictionary, key, firstStance);
        ContainerLoadDataPatches.AddDictionaryEntry(dictionary, key, laterStance);

        var stances = Assert.IsType<Dictionary<(IFaction, IFaction), StanceLink>>(dictionary);
        Assert.Single(stances);
        Assert.Same(laterStance, stances[key]);
    }

    [Fact]
    public void AddDictionaryEntry_DuplicateOrdinaryKey_PreservesVanillaFailure()
    {
        IDictionary dictionary = new Dictionary<string, int>();

        ContainerLoadDataPatches.AddDictionaryEntry(dictionary, "duplicate", 1);

        Assert.Throws<ArgumentException>(() =>
            ContainerLoadDataPatches.AddDictionaryEntry(dictionary, "duplicate", 2));
    }

    [Fact]
    public void Transpiler_ReplacesDictionaryAddCall()
    {
        var dictionaryAdd = AccessTools.Method(
            typeof(IDictionary),
            nameof(IDictionary.Add),
            new[] { typeof(object), typeof(object) });
        var replacement = AccessTools.Method(
            typeof(ContainerLoadDataPatches),
            nameof(ContainerLoadDataPatches.AddDictionaryEntry));
        var instructions = new[]
        {
            new CodeInstruction(OpCodes.Nop),
            new CodeInstruction(OpCodes.Callvirt, dictionaryAdd),
            new CodeInstruction(OpCodes.Ret),
        };

        var result = ContainerLoadDataPatches.Transpiler(instructions).ToList();

        Assert.Equal(OpCodes.Call, result[1].opcode);
        Assert.Equal(replacement, result[1].operand);
    }

    [Fact]
    public void Transpiler_RealFillObject_ReplacesDictionaryAddCall()
    {
        var fillObject = AccessTools.Method(
            typeof(ContainerLoadData),
            nameof(ContainerLoadData.FillObject));
        var replacement = AccessTools.Method(
            typeof(ContainerLoadDataPatches),
            nameof(ContainerLoadDataPatches.AddDictionaryEntry));

        var result = ContainerLoadDataPatches.Transpiler(
            PatchProcessor.GetOriginalInstructions(fillObject)).ToList();

        Assert.Single(result, instruction => instruction.Calls(replacement));
    }

    [Fact]
    public void Transpiler_AlreadyReplacedCall_RemainsStable()
    {
        var replacement = AccessTools.Method(
            typeof(ContainerLoadDataPatches),
            nameof(ContainerLoadDataPatches.AddDictionaryEntry));
        var instructions = new[]
        {
            new CodeInstruction(OpCodes.Nop),
            new CodeInstruction(OpCodes.Call, replacement),
            new CodeInstruction(OpCodes.Ret),
        };

        var result = ContainerLoadDataPatches.Transpiler(instructions).ToList();

        Assert.Equal(OpCodes.Call, result[1].opcode);
        Assert.Equal(replacement, result[1].operand);
    }

    [Fact]
    public void Transpiler_MissingDictionaryAddCall_FailsPatchApplication()
    {
        var instructions = new[] { new CodeInstruction(OpCodes.Ret) };

        Assert.Throws<InvalidOperationException>(() =>
            ContainerLoadDataPatches.Transpiler(instructions).ToList());
    }
}
