using GameInterface.Services.PartyVisuals.Patches;
using SandBox.View.Map.Visuals;
using Xunit;

namespace GameInterface.Tests.Services.PartyVisuals;

public class MobilePartyVisualManagerPatchesTests
{
    [Fact]
    public void PrepareDirtyPartyVisualBuffer_AtVanillaBoundary_ResetsCounterWithoutGrowing()
    {
        int dirtyCount = 12;
        var buffer = new MobilePartyVisual[2500];
        var originalBuffer = buffer;

        MobilePartyVisualManagerPatches.PrepareDirtyPartyVisualBuffer(
            ref dirtyCount,
            ref buffer,
            2500);

        Assert.Equal(-1, dirtyCount);
        Assert.Same(originalBuffer, buffer);
    }

    [Fact]
    public void PrepareDirtyPartyVisualBuffer_AboveVanillaBoundary_GrowsForAffectedSave()
    {
        int dirtyCount = 0;
        var buffer = new MobilePartyVisual[2500];

        MobilePartyVisualManagerPatches.PrepareDirtyPartyVisualBuffer(
            ref dirtyCount,
            ref buffer,
            2556);

        Assert.Equal(-1, dirtyCount);
        Assert.True(buffer.Length >= 2556);
    }

    [Fact]
    public void PrepareDirtyPartyVisualBuffer_WhenAlreadyLargeEnough_DoesNotShrink()
    {
        int dirtyCount = 4;
        var buffer = new MobilePartyVisual[6000];
        var originalBuffer = buffer;

        MobilePartyVisualManagerPatches.PrepareDirtyPartyVisualBuffer(
            ref dirtyCount,
            ref buffer,
            1000);

        Assert.Equal(-1, dirtyCount);
        Assert.Equal(6000, buffer.Length);
        Assert.Same(originalBuffer, buffer);
    }

    [Fact]
    public void PrepareDirtyPartyVisualBuffer_AcrossRepeatedCalls_ReusesGrownBufferAndResetsCounter()
    {
        int dirtyCount = 0;
        var buffer = new MobilePartyVisual[2500];

        MobilePartyVisualManagerPatches.PrepareDirtyPartyVisualBuffer(
            ref dirtyCount,
            ref buffer,
            2556);
        var grownBuffer = buffer;
        dirtyCount = 37;

        MobilePartyVisualManagerPatches.PrepareDirtyPartyVisualBuffer(
            ref dirtyCount,
            ref buffer,
            2556);

        Assert.Equal(-1, dirtyCount);
        Assert.Same(grownBuffer, buffer);
    }
}
