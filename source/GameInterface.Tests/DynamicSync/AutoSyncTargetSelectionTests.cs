using GameInterface.Utils;
using GameInterface.Utils.LocalEvents;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.AutoSync;

/// <summary>
/// Verifies that AutoSync drops dangerous trivial stubs while preserving nontrivial methods whose
/// existing Harmony detours can be required JIT anti-inlining boundaries.
/// </summary>
public class AutoSyncTargetSelectionTests
{
    [Fact]
    public void Keeps_nontrivial_methods_but_drops_empty_and_constant_stubs()
    {
        var targets = GenericPatches<SelectionPatch, SelectionSample>
            .TranspilerTargets(true)
            .ToArray();

        Assert.Contains(typeof(SelectionSample).GetConstructor(Type.EmptyTypes)!, targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.StoreSyncedScalar)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.MutateSyncedList)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.MutateSyncedArray)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.MutateSyncedProperty)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.AssignSyncedProperty)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.ReadSyncedList)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.ReadSyncedProperty)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.ForwardToScalarStore)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.ReadSyncedScalar)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.ReadSyncedArray)), targets);
        Assert.Contains(SampleMethod(nameof(SelectionSample.StoreOtherScalar)), targets);

        Assert.DoesNotContain(SampleMethod(nameof(SelectionSample.ConstantFalse)), targets);
        Assert.DoesNotContain(SampleMethod(nameof(SelectionSample.ConstantLong)), targets);
        Assert.DoesNotContain(SampleMethod(nameof(SelectionSample.ConstantNull)), targets);
        Assert.DoesNotContain(SampleMethod(nameof(SelectionSample.Empty)), targets);
    }

    [Fact]
    public void Drops_the_reported_clan_interface_getter()
    {
        var interfaceGetter = typeof(IFaction).GetProperty(nameof(IFaction.IsKingdomFaction))!.GetMethod!;
        var interfaceMap = typeof(Clan).GetInterfaceMap(typeof(IFaction));
        var interfaceIndex = Array.IndexOf(interfaceMap.InterfaceMethods, interfaceGetter);
        Assert.True(interfaceIndex >= 0);

        var reportedGetter = interfaceMap.TargetMethods[interfaceIndex];
        var targets = GenericPatches<ClanSelectorPatch, Clan>
            .TranspilerTargets(true)
            .ToArray();

        Assert.NotEmpty(targets);
        Assert.DoesNotContain(reportedGetter, targets);
    }

    [Fact]
    public void Dead_fief_field_registration_retains_nontrivial_methods()
    {
        var targets = GenericPatches<FiefSelectorPatch, Fief>
            .TranspilerTargets(true)
            .ToArray();

        Assert.NotEmpty(targets);
    }

    [Fact]
    public void Uses_the_composed_instruction_stream_and_retains_replacement_calls()
    {
        var introducedWrite = ComposedMethod(nameof(ComposedSelectionSample.IntroducedWrite));
        var consumedWrite = ComposedMethod(nameof(ComposedSelectionSample.ConsumedWrite));
        var harmony = new Harmony($"{nameof(AutoSyncTargetSelectionTests)}.{Guid.NewGuid()}");

        try
        {
            harmony.Patch(
                introducedWrite,
                transpiler: new HarmonyMethod(
                    typeof(ExistingCompositionTranspilers),
                    nameof(ExistingCompositionTranspilers.IntroduceWrite)));
            harmony.Patch(
                consumedWrite,
                transpiler: new HarmonyMethod(
                    typeof(ExistingCompositionTranspilers),
                    nameof(ExistingCompositionTranspilers.ConsumeWrite)));

            var targets = GenericPatches<ComposedSelectionPatch, ComposedSelectionSample>
                .TranspilerTargets(false)
                .ToArray();

            Assert.Contains(introducedWrite, targets);
            Assert.Contains(consumedWrite, targets);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static MethodInfo SampleMethod(string name) => typeof(SelectionSample).GetMethod(name)!;

    private static MethodInfo ComposedMethod(string name) => typeof(ComposedSelectionSample).GetMethod(name)!;

    /// <summary>
    /// Provides representative scalar, collection, array, and property IL shapes.
    /// </summary>
    private sealed class SelectionSample
    {
        public int SyncedScalar;
        public int OtherScalar;
        public readonly List<int> SyncedList = new List<int>();
        public string[] SyncedArray = new string[1];
        public List<int> SyncedProperty { get; set; } = new List<int>();

        public SelectionSample()
        {
            SyncedScalar = 1;
        }

        public void StoreSyncedScalar(int value) => SyncedScalar = value;
        public int ReadSyncedScalar() => SyncedScalar;
        public void StoreOtherScalar(int value) => OtherScalar = value;
        public bool ConstantFalse() => false;
        public long ConstantLong() => 4_294_967_296L;
        public object? ConstantNull() => null;
        public void Empty() { }
        public void MutateSyncedList() => SyncedList.Add(1);
        public int ReadSyncedList() => SyncedList.Count;
        public void MutateSyncedArray() => SyncedArray[0] = "updated";
        public string ReadSyncedArray() => SyncedArray[0];
        public void MutateSyncedProperty() => SyncedProperty.Add(1);
        public List<int> ReadSyncedProperty() => SyncedProperty;
        public void AssignSyncedProperty(List<int> values) => SyncedProperty = values;
        public void ForwardToScalarStore(int value) => StoreSyncedScalar(value);
    }

    /// <summary>
    /// Uses the same generic transpilers emitted by AutoSync for the sample members.
    /// </summary>
    private sealed class SelectionPatch : GenericPatches<SelectionPatch, SelectionSample>
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SyncedScalarTranspiler(IEnumerable<CodeInstruction> instructions) =>
            FieldTranspiler<int, GenericEvent<SelectionSample, int>>(instructions, nameof(SelectionSample.SyncedScalar));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SyncedListTranspiler(IEnumerable<CodeInstruction> instructions) =>
            ListFieldChangeTranspiler<int, GenericEvent<SelectionSample, int>, GenericEvent<SelectionSample, int>>(
                instructions,
                nameof(SelectionSample.SyncedList));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SyncedArrayTranspiler(IEnumerable<CodeInstruction> instructions) =>
            ArrayFieldChangeTranspiler<string, SelectionArrayChanged>(instructions, nameof(SelectionSample.SyncedArray));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SyncedPropertyTranspiler(IEnumerable<CodeInstruction> instructions) =>
            ListPropertyChangeTranspiler<int, GenericEvent<SelectionSample, int>, GenericEvent<SelectionSample, int>>(
                instructions,
                nameof(SelectionSample.SyncedProperty));
    }

    /// <summary>
    /// Supplies the concrete event type required by the array transpiler.
    /// </summary>
    private sealed record SelectionArrayChanged : GenericArrayChangedEvent<SelectionSample, string>
    {
        public SelectionArrayChanged(SelectionSample instance, string value, int index) : base(instance, value, index)
        {
        }
    }

    /// <summary>
    /// Represents the registered Clan field while leaving unrelated accessors untouched.
    /// </summary>
    private sealed class ClanSelectorPatch : GenericPatches<ClanSelectorPatch, Clan>
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> EliminatedTranspiler(IEnumerable<CodeInstruction> instructions) =>
            FieldTranspiler<bool, GenericEvent<Clan, bool>>(instructions, nameof(Clan._isEliminated));
    }

    /// <summary>
    /// Represents Fief's dead GarrisonPartyComponent field registration.
    /// </summary>
    private sealed class FiefSelectorPatch : GenericPatches<FiefSelectorPatch, Fief>
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> GarrisonTranspiler(IEnumerable<CodeInstruction> instructions) =>
            FieldTranspiler<GarrisonPartyComponent, GenericEvent<Fief, GarrisonPartyComponent>>(
                instructions,
                nameof(Fief.GarrisonPartyComponent));
    }

    private sealed class ComposedSelectionSample
    {
        public int SyncedScalar;

        public void IntroducedWrite(int value)
        {
        }

        public void ConsumedWrite(int value) => SyncedScalar = value;
    }

    private sealed class ComposedSelectionPatch : GenericPatches<ComposedSelectionPatch, ComposedSelectionSample>
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SyncedScalarTranspiler(IEnumerable<CodeInstruction> instructions) =>
            FieldTranspiler<int, GenericEvent<ComposedSelectionSample, int>>(
                instructions,
                nameof(ComposedSelectionSample.SyncedScalar));
    }

    private static class ExistingCompositionTranspilers
    {
        public static IEnumerable<CodeInstruction> IntroduceWrite(IEnumerable<CodeInstruction> instructions)
        {
            var field = AccessTools.Field(
                typeof(ComposedSelectionSample),
                nameof(ComposedSelectionSample.SyncedScalar));

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ret)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Stfld, field);
                }

                yield return instruction;
            }
        }

        public static IEnumerable<CodeInstruction> ConsumeWrite(IEnumerable<CodeInstruction> instructions)
        {
            var field = AccessTools.Field(
                typeof(ComposedSelectionSample),
                nameof(ComposedSelectionSample.SyncedScalar));
            var replacement = AccessTools.Method(
                typeof(ExistingCompositionTranspilers),
                nameof(StoreWithoutVisibleFieldInstruction));

            foreach (var instruction in instructions)
            {
                yield return instruction.StoresField(field)
                    ? new CodeInstruction(OpCodes.Call, replacement)
                    : instruction;
            }
        }

        private static void StoreWithoutVisibleFieldInstruction(ComposedSelectionSample instance, int value) =>
            instance.SyncedScalar = value;
    }
}
