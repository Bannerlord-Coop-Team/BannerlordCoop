using GameInterface.Services.Villages;
using Xunit;

namespace GameInterface.Tests.Services.Villages;

public class ForceTransferScreenTrackerTests
{
    [Fact]
    public void Claim_ExactRoster_ReturnsIdOnce()
    {
        var roster = new object();
        ForceTransferScreenTracker.NoteLootScreenOpened("req-1", roster);

        Assert.True(ForceTransferScreenTracker.TryClaimForceTransferId(roster, out var claimed));
        Assert.Equal("req-1", claimed);
        Assert.False(ForceTransferScreenTracker.TryClaimForceTransferId(roster, out _));
    }

    [Fact]
    public void Claim_MismatchedRoster_PreservesSlot()
    {
        var roster = new object();
        ForceTransferScreenTracker.NoteLootScreenOpened("req-1", roster);

        Assert.False(ForceTransferScreenTracker.TryClaimForceTransferId(new object(), out _));
        Assert.True(ForceTransferScreenTracker.TryClaimForceTransferId(roster, out var claimed));
        Assert.Equal("req-1", claimed);
    }

    [Fact]
    public void Note_NewerScreen_OverwritesSlot()
    {
        var first = new object();
        var second = new object();
        ForceTransferScreenTracker.NoteLootScreenOpened("req-1", first);
        ForceTransferScreenTracker.NoteLootScreenOpened("req-2", second);

        Assert.False(ForceTransferScreenTracker.TryClaimForceTransferId(first, out _));
        Assert.True(ForceTransferScreenTracker.TryClaimForceTransferId(second, out var claimed));
        Assert.Equal("req-2", claimed);
    }

    [Fact]
    public void Clear_DropsPendingAttribution()
    {
        var roster = new object();
        ForceTransferScreenTracker.NoteLootScreenOpened("req-1", roster);
        ForceTransferScreenTracker.Clear();

        Assert.False(ForceTransferScreenTracker.TryClaimForceTransferId(roster, out _));
    }

    [Fact]
    public void Note_NullArgs_Ignored()
    {
        ForceTransferScreenTracker.Clear();
        ForceTransferScreenTracker.NoteLootScreenOpened(null, new object());
        ForceTransferScreenTracker.NoteLootScreenOpened("req-1", null);

        Assert.False(ForceTransferScreenTracker.TryClaimForceTransferId(new object(), out _));
    }
}
