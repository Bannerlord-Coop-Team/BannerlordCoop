using GameInterface.Services.GameState.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
#if NETFRAMEWORK
using System.Runtime.Remoting.Proxies;
using System.Runtime.Remoting.Messaging;
#endif
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

/// <summary>Checks the installed cache reader's IL contract and its file-sharing boundary.</summary>
public class NavigationCacheDeserializePatchTests
{
    private static readonly MethodInfo ExclusiveOpen = AccessTools.Method(
        typeof(File), nameof(File.Open), new[] { typeof(string), typeof(FileMode), typeof(FileAccess) });

    private static readonly MethodInfo SharedOpen = AccessTools.Method(
        typeof(File), nameof(File.Open), new[] { typeof(string), typeof(FileMode), typeof(FileAccess), typeof(FileShare) });

    // The inherited game-used method must change only its single read-open instruction.
    [Fact]
    public void Transpiler_InstalledClosedDeserialize_ChangesOnlyReadSharing()
    {
        var target = AccessTools.Method(typeof(NavigationCache<Settlement>), nameof(NavigationCache<Settlement>.Deserialize), new[] { typeof(string) });
        Assert.Equal(typeof(NavigationCache<Settlement>), target.DeclaringType);
        Assert.False(target.ContainsGenericParameters);
        var inherited = typeof(SandBoxNavigationCache).GetMethod("Deserialize", new[] { typeof(string) })!;
        Assert.Equal(target.DeclaringType, inherited.DeclaringType);
        Assert.Equal(target.MethodHandle, inherited.MethodHandle);
        var original = PatchProcessor.GetOriginalInstructions(target).ToList();
        var call = Assert.Single(original, instruction => instruction.Calls(ExclusiveOpen));
        var index = original.IndexOf(call);

        var result = NavigationCacheDeserializePatch.TranspileReadOpen(original).ToList();

        Assert.Equal(original.Count + 1, result.Count);
        Assert.Equal(OpCodes.Ldarg_1, result[index - 3].opcode);
        Assert.True(result[index - 2].LoadsConstant((int)FileMode.Open));
        Assert.True(result[index - 1].LoadsConstant((int)FileAccess.Read));
        Assert.True(result[index].LoadsConstant((int)FileShare.Read));
        Assert.True(result[index + 1].Calls(SharedOpen));
        Assert.DoesNotContain(result, instruction => instruction.Calls(ExclusiveOpen));
        for (var i = 0; i < original.Count; i++)
        {
            if (i != index) Assert.Same(original[i], result[i < index ? i : i + 1]);
        }
        Assert.True(call.Calls(ExclusiveOpen));
    }

    // This same test runs in the linked net48 project, where generic detour installation failed.
    [Fact]
    public void Patch_Caller_InstallsWithoutDetouringGenericDeserialize()
    {
        var harmony = new Harmony("NavigationCacheDeserializePatchTests.Caller");
        var caller = AccessTools.Method(typeof(SettlementPositionScript), "ReadNavigationCacheOnGameLoad");
        var deserialize = AccessTools.Method(typeof(NavigationCache<Settlement>), "Deserialize");
        var originalPatches = Harmony.GetPatchInfo(deserialize);
        try
        {
            harmony.CreateClassProcessor(typeof(NavigationCacheDeserializePatch)).Patch();
            Assert.Contains(Harmony.GetPatchInfo(caller).Transpilers, patch => patch.owner == harmony.Id);
            Assert.Same(originalPatches, Harmony.GetPatchInfo(deserialize));
        }
        finally
        {
            harmony.Unpatch(caller, HarmonyPatchType.All, harmony.Id);
        }
    }

    // Preserve the fresh receiver, return value and call metadata while redirecting the only decode call.
    [Fact]
    public void RedirectDeserialize_InstalledCaller_ChangesOnlyNonvirtualDecodeCall()
    {
        var caller = AccessTools.Method(typeof(SettlementPositionScript), "ReadNavigationCacheOnGameLoad");
        var deserialize = AccessTools.Method(typeof(NavigationCache<Settlement>), "Deserialize");
        Assert.False(deserialize.IsVirtual);
        var original = PatchProcessor.GetOriginalInstructions(caller).ToList();
        var call = Assert.Single(original, instruction => instruction.Calls(deserialize));
        var index = original.IndexOf(call);
        Assert.Equal(OpCodes.Callvirt, call.opcode);
        var constructor = Assert.Single(original, instruction => instruction.opcode == OpCodes.Newobj);
        Assert.Equal(typeof(SandBoxNavigationCache), ((ConstructorInfo)constructor.operand).DeclaringType);
        var label = new DynamicMethod("CallerLabel", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
        call.labels.Add(label);
        var block = new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock);
        call.blocks.Add(block);

        var result = NavigationCacheDeserializePatch.RedirectDeserialize(original).ToList();

        Assert.Equal(original.Count, result.Count);
        Assert.Equal(OpCodes.Call, result[index].opcode);
        Assert.True(result[index].Calls(AccessTools.Method(typeof(NavigationCacheDeserializePatch), "DeserializeShared")));
        Assert.Equal(call.labels, result[index].labels);
        Assert.Equal(call.blocks, result[index].blocks);
        for (var i = 0; i < original.Count; i++)
        {
            if (i != index) Assert.Same(original[i], result[i]);
        }
        Assert.True(call.Calls(deserialize));
        Assert.Equal(OpCodes.Callvirt, call.opcode);
    }

    // Missing or duplicated caller sites must not be partially rewritten.
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RedirectDeserialize_UnexpectedCallCount_ThrowsWithoutChangingInput(int count)
    {
        var deserialize = AccessTools.Method(typeof(NavigationCache<Settlement>), "Deserialize");
        var instructions = Enumerable.Range(0, count).Select(_ => new CodeInstruction(OpCodes.Callvirt, deserialize)).ToList();

        var error = Assert.Throws<InvalidOperationException>(() => NavigationCacheDeserializePatch.RedirectDeserialize(instructions).ToList());

        Assert.Contains("ReadNavigationCacheOnGameLoad", error.Message);
        Assert.All(instructions, instruction => Assert.True(instruction.Calls(deserialize)));
    }

#if NETFRAMEWORK
    // Run the generated original decoder on CLR4 with a managed CRC-only scene, never a native campaign.
    [Fact]
    public void ReverseDecoder_Framework_OverlappingReaderDecodesEmptyCacheAndReleasesHandle()
    {
        Assert.Equal(".NETFramework", AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName.Split(',')[0]);
        Assert.True(Environment.Is64BitProcess);
        var harmony = new Harmony("NavigationCacheDeserializePatchTests.FrameworkDecoder");
        var caller = AccessTools.Method(typeof(SettlementPositionScript), "ReadNavigationCacheOnGameLoad");
        var originalCampaign = Campaign.Current;
        var path = Path.GetTempFileName();
        try
        {
            harmony.PatchAllUncategorized(typeof(NavigationCacheDeserializePatch).Assembly);
            var campaign = (Campaign)FormatterServices.GetUninitializedObject(typeof(Campaign));
            var scene = new CrcOnlySceneProxy();
            AccessTools.Field(typeof(Campaign), "_mapSceneWrapper").SetValue(campaign, scene.GetTransparentProxy());
            AccessTools.PropertySetter(typeof(Campaign), "Current").Invoke(null, new object[] { campaign });
            var cache = (SandBoxNavigationCache)FormatterServices.GetUninitializedObject(typeof(SandBoxNavigationCache));
            // Two CRC values and three empty dictionary counts form a valid empty vanilla cache.
            File.WriteAllBytes(path, new byte[20]);
            using (var held = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() => cache.Deserialize(path));
                NavigationCacheDeserializePatch.DeserializeShared(cache, path);
                Assert.Equal(2, scene.Calls);
                foreach (var field in new[] { "_settlementToSettlementDistanceWithLandRatio", "_fortificationNeighbors", "_closestSettlementsToFaceIndices" })
                    Assert.Empty((System.Collections.IDictionary)AccessTools.Field(typeof(SandBoxNavigationCache), field).GetValue(cache));
                Assert.Throws<IOException>(() =>
                {
                    using var writer = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                });
            }
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(exclusive.CanWrite);
        }
        finally
        {
            AccessTools.PropertySetter(typeof(Campaign), "Current").Invoke(null, new object[] { originalCampaign });
            harmony.Unpatch(caller, HarmonyPatchType.All, harmony.Id);
            File.Delete(path);
        }
    }

    // Supply only the two managed CRC queries that the installed decoder requires.
    private sealed class CrcOnlySceneProxy : RealProxy
    {
        public int Calls { get; private set; }

        // Implement the scene interface without initializing TaleWorlds native engine state.
        public CrcOnlySceneProxy() : base(typeof(TaleWorlds.CampaignSystem.Map.IMapScene)) { }

        // Reject unexpected engine requests rather than fabricating scene behavior.
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            Assert.Contains(call.MethodName, new[] { "GetSceneXmlCrc", "GetSceneNavigationMeshCrc" });
            Calls++;
            return new ReturnMessage((uint)0, null, 0, call.LogicalCallContext, call);
        }
    }
#endif

    // Branch entry needs the new argument, while exception exit still follows the open call.
    [Fact]
    public void Transpiler_LabeledCall_PreservesLabelsAndExceptionBoundaries()
    {
        var generator = new DynamicMethod("Labels", typeof(void), Type.EmptyTypes).GetILGenerator();
        var label = generator.DefineLabel();
        var instructions = ReadOpenInstructions();
        var call = instructions[3];
        call.labels.Add(label);
        var begin = new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock);
        var end = new ExceptionBlock(ExceptionBlockType.EndExceptionBlock);
        call.blocks.Add(begin);
        call.blocks.Add(end);

        var result = NavigationCacheDeserializePatch.TranspileReadOpen(instructions).ToList();

        Assert.Equal(new[] { label }, result[3].labels);
        Assert.Equal(new[] { begin }, result[3].blocks);
        Assert.Empty(result[4].labels);
        Assert.Equal(new[] { end }, result[4].blocks);
        Assert.Equal(new[] { label }, call.labels);
        Assert.Equal(new[] { begin, end }, call.blocks);
        Assert.True(call.Calls(ExclusiveOpen));
    }

    // Unsupported reader shapes must fail before any input instruction is rewritten.
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("path")]
    [InlineData("mode")]
    [InlineData("access")]
    public void Transpiler_UnexpectedShape_ThrowsWithoutChangingInput(string shape)
    {
        var instructions = ReadOpenInstructions();
        switch (shape)
        {
            case "missing": instructions.RemoveAt(3); break;
            case "duplicate": instructions.AddRange(ReadOpenInstructions()); break;
            case "path": instructions[0] = new CodeInstruction(OpCodes.Ldnull); break;
            case "mode": instructions[1] = new CodeInstruction(OpCodes.Ldc_I4, (int)FileMode.Create); break;
            case "access": instructions[2] = new CodeInstruction(OpCodes.Ldc_I4, (int)FileAccess.Write); break;
        }
        var before = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();

        var error = Assert.Throws<InvalidOperationException>(() => NavigationCacheDeserializePatch.TranspileReadOpen(instructions).ToList());

        Assert.Contains("NavigationCache<Settlement>.Deserialize", error.Message);
        Assert.Equal(before.Count, instructions.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].opcode, instructions[i].opcode);
            Assert.Equal(before[i].operand, instructions[i].operand);
        }
    }

    // Execute the transformed boundary with a branch to the call, not a hand-written shared open.
    [Fact]
    public void TransformedReadOpen_OverlappingReaders_ReadSameBytesRejectWriterAndReleaseHandles()
    {
        var method = new DynamicMethod("SharedCacheRead", typeof(FileStream), new[] { typeof(object), typeof(string) });
        var generator = method.GetILGenerator();
        var label = generator.DefineLabel();
        var instructions = ReadOpenInstructions();
        instructions[3].labels.Add(label);
        var transformed = NavigationCacheDeserializePatch.TranspileReadOpen(instructions).ToList();
        transformed.Insert(3, new CodeInstruction(OpCodes.Br, label));
        transformed.Add(new CodeInstruction(OpCodes.Ret));
        foreach (var instruction in transformed)
        {
            foreach (var target in instruction.labels) generator.MarkLabel(target);
            if (instruction.operand is MethodInfo methodInfo) generator.Emit(instruction.opcode, methodInfo);
            else if (instruction.operand is int value) generator.Emit(instruction.opcode, value);
            else if (instruction.operand is Label branch) generator.Emit(instruction.opcode, branch);
            else generator.Emit(instruction.opcode);
        }
        var open = (Func<object, string, FileStream>)method.CreateDelegate(typeof(Func<object, string, FileStream>));
        var path = Path.GetTempFileName();
        var bytes = new byte[] { 0, 1, 127, 128, 255 };
        try
        {
            File.WriteAllBytes(path, bytes);
            using (var first = new BinaryReader(open(null!, path)))
            using (var second = new BinaryReader(open(null!, path)))
            {
                Assert.Equal(bytes, first.ReadBytes(bytes.Length));
                Assert.Equal(bytes, second.ReadBytes(bytes.Length));
                Assert.Throws<IOException>(() =>
                {
                    using var writer = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                });
            }
            using var exclusiveWriter = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);
            Assert.True(exclusiveWriter.CanWrite);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Match the installed instance method's path, mode and access argument sequence.
    private static List<CodeInstruction> ReadOpenInstructions() => new List<CodeInstruction>
    {
        new CodeInstruction(OpCodes.Ldarg_1),
        new CodeInstruction(OpCodes.Ldc_I4_3),
        new CodeInstruction(OpCodes.Ldc_I4_1),
        new CodeInstruction(OpCodes.Call, ExclusiveOpen),
    };
}
