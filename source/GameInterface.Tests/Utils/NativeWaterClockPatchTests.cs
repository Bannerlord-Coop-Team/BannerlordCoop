using Common;
using GameInterface.Utils;
using System;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Utils;

[Collection(ModInformationRoleCollection.Name)]
public class NativeWaterClockPatchTests : IDisposable
{
    // TaleWorlds.Native.dll v1.4.8 (Steam), RVA 0x1DA8B4..0x1DA8E7 (file offset 0x1D9CB4): the water update's clock load.
    private const string V148Bytes =
        "48 8B CB E8 D4 FE FF FF 48 63 83 64 60 00 00 48 8D 2D E6 80 BC 00 0F 57 C9 F3 48 0F 2A 4C C5 00 " +
        "F3 0F 59 0D A8 06 95 00 48 8B 8B 28 06 00 00 E8 48 49 13 00";
    private const long FixtureRva = 0x1DA8B4;
    private const long MatchRva = 0x1DA8BC;

    // Restores the client role the other tests in this collection assume.
    public void Dispose() => ModInformation.IsServer = false;

    [Fact]
    public void Signature_MatchesOnceInTheV148ClockLoad()
    {
        var data = Bytes(V148Bytes);

        var matches = NativeWaterClockPatch.FindMatches(data, NativeWaterClockPatch.Signature, 2);

        Assert.Equal(new[] { (int)(MatchRva - FixtureRva) }, matches);
    }

    [Fact]
    public void Signature_SpansTheTwoReplacedInstructionsAtThePatchOffset()
    {
        var data = Bytes(V148Bytes);
        int site = (int)(MatchRva - FixtureRva) + NativeWaterClockPatch.PatchOffset;

        // cvtsi2ss xmm1,qword [rbp+rax*8] then mulss xmm1,[rip+k]
        Assert.Equal(Bytes("F3 48 0F 2A 4C C5 00 F3 0F 59 0D A8 06 95 00"),
            data.Skip(site).Take(NativeWaterClockPatch.PatchLength).ToArray());
    }

    [Fact]
    public void Signature_DoesNotMatchWhenARebuildMovesTheSceneTimerField()
    {
        // The same load reading [rbx+6068h] instead: what an engine rebuild that shifts the scene layout looks like.
        var data = Bytes(V148Bytes);
        data[(int)(MatchRva - FixtureRva) + 3] = 0x68;

        Assert.Empty(NativeWaterClockPatch.FindMatches(data, NativeWaterClockPatch.Signature, 2));
    }

    [Fact]
    public void FindMatches_StopsAtTheLimitSoTwoMatchesDisableThePatch()
    {
        var data = Bytes(V148Bytes + " " + V148Bytes + " " + V148Bytes);

        Assert.Equal(2, NativeWaterClockPatch.FindMatches(data, NativeWaterClockPatch.Signature, 2).Count);
    }

    [Fact]
    public void FindMatches_IgnoresATruncatedMatchAtTheEnd()
    {
        var data = Bytes(V148Bytes).Take((int)(MatchRva - FixtureRva) + 20).ToArray();

        Assert.Empty(NativeWaterClockPatch.FindMatches(data, NativeWaterClockPatch.Signature, 2));
    }

    [Fact]
    public void Parse_ReadsHexAndWildcards()
    {
        Assert.Equal(new byte?[] { 0x48, null, 0x0D }, NativeWaterClockPatch.Parse("48 ?? 0D"));
    }

    [Fact]
    public void RipTargets_ResolveTheV148TickTableAndScale()
    {
        var data = Bytes(V148Bytes);
        int match = (int)(MatchRva - FixtureRva);

        long table = NativeWaterClockPatch.ResolveRipTarget(MatchRva + NativeWaterClockPatch.TableInstructionEnd,
            BitConverter.ToInt32(data, match + NativeWaterClockPatch.TableDisplacementOffset));
        long scale = NativeWaterClockPatch.ResolveRipTarget(MatchRva + NativeWaterClockPatch.ScaleInstructionEnd,
            BitConverter.ToInt32(data, match + NativeWaterClockPatch.ScaleDisplacementOffset));

        Assert.Equal(0xDA29B0, table);
        Assert.Equal(0xB2AF84, scale);
    }

    [Fact]
    public void TryBuildRedirect_EncodesMovssFromTheSlotAndANop()
    {
        const long site = 0x7FF6_0000_1000;
        const long slot = 0x7FF5_FFFF_0000;

        Assert.True(NativeWaterClockPatch.TryBuildRedirect(site, slot, out var code));

        Assert.Equal(NativeWaterClockPatch.PatchLength, code.Length);
        Assert.Equal(Bytes("F3 0F 10 0D"), code.Take(4).ToArray());
        Assert.Equal(slot - (site + 8), BitConverter.ToInt32(code, 4));
        Assert.Equal(Bytes("0F 1F 80 00 00 00 00"), code.Skip(8).ToArray());
    }

    [Fact]
    public void TryBuildRedirect_RefusesASlotBeyondRel32Reach()
    {
        const long site = 0x7FF6_0000_1000;

        Assert.False(NativeWaterClockPatch.TryBuildRedirect(site, site - 0x9000_0000L, out var code));
        Assert.Null(code);
    }

    [Fact]
    public void TryInstall_OnAServer_NeverInstalls()
    {
        ModInformation.IsServer = true;
        var waterClock = new WaterClock();

        Assert.False(waterClock.TryInstall());

        Assert.False(waterClock.IsInstalled);
        Assert.Equal("disabled: not a client process", waterClock.Status);
    }

    [Fact]
    public void TryInstall_WithoutTheEngineModule_StaysVanillaAndEveryCallDoesNothing()
    {
        ModInformation.IsServer = false;
        var waterClock = new WaterClock();

        Assert.False(waterClock.TryInstall());

        Assert.False(waterClock.IsInstalled);
        Assert.False(waterClock.TryReadVanillaMissionTime(out _));
        Assert.False(waterClock.TryRedirect(1f));
        waterClock.SetWaterTime(1f);
        waterClock.Restore();
        Assert.False(waterClock.IsRedirected);
    }

    // "48 8B ..." as bytes.
    private static byte[] Bytes(string hex) =>
        hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(t => Convert.ToByte(t, 16)).ToArray();
}
