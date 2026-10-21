#if DEBUG
using Common.LiveTesting;

namespace Common.Tests.LiveTesting;

/// <summary>Checks retries after refused joins and completed client teardown.</summary>
public class LiveTestClientJoinStateTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void IncompleteTeardownDoesNotRearmJoin(bool atMainMenu, bool campaignLoaded, bool sessionActive)
    {
        var state = new LiveTestClientJoinState();
        Assert.True(state.TryBegin());
        Assert.False(state.Refresh(atMainMenu, campaignLoaded, sessionActive));
        Assert.True(state.Attempted);
        Assert.False(state.TryBegin());
    }

    [Fact]
    public void CompletedTeardownRearmsJoinAfterInvoluntaryDisconnect()
    {
        var state = new LiveTestClientJoinState();
        Assert.True(state.TryBegin());
        state.CompleteStart(true);
        Assert.True(state.Refresh(true, false, false));
        Assert.False(state.Attempted);
        Assert.True(state.TryBegin());
        Assert.False(state.TryBegin());
    }

    [Fact]
    public void RefusedStartAllowsAnotherAttempt()
    {
        var state = new LiveTestClientJoinState();
        Assert.True(state.TryBegin());
        state.CompleteStart(false);
        Assert.False(state.Attempted);
        Assert.True(state.TryBegin());
    }
}
#endif
