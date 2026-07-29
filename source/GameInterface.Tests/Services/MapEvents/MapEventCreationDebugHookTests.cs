#if DEBUG
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Start;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventCreationDebugHookTests
{
    [Fact]
    public void TryConsume_MatchingRequest_RejectsExactlyOnce()
    {
        var hook = new MapEventCreationDebugHook();
        hook.Arm("attacker", "defender");
        var request = new NetworkRequestCreateMapEvent("request", "attacker", "defender", default(BattleCreationFlags));

        Assert.True(hook.TryConsume(request));
        Assert.False(hook.TryConsume(request));
        Assert.False(hook.IsArmed);
        Assert.Equal(1, hook.RejectionCount);
    }

    [Fact]
    public void TryConsume_DifferentParties_LeavesMatchingRejectionArmed()
    {
        var hook = new MapEventCreationDebugHook();
        hook.Arm("attacker", "defender");
        var differentRequest = new NetworkRequestCreateMapEvent("request-1", "other", "defender", default(BattleCreationFlags));
        var matchingRequest = new NetworkRequestCreateMapEvent("request-2", "attacker", "defender", default(BattleCreationFlags));

        Assert.False(hook.TryConsume(differentRequest));
        Assert.True(hook.IsArmed);
        Assert.True(hook.TryConsume(matchingRequest));
        Assert.Equal(1, hook.RejectionCount);
    }

    [Fact]
    public void TryConsume_ReversedParties_ConsumesTheArmedPair()
    {
        var hook = new MapEventCreationDebugHook();
        hook.Arm("player", "hostile");
        var request = new NetworkRequestCreateMapEvent("request", "hostile", "player", default(BattleCreationFlags));

        Assert.True(hook.TryConsume(request));
        Assert.Equal(1, hook.RejectionCount);
    }

    [Fact]
    public void Clear_ResetsPerFixtureRejectionCount()
    {
        var hook = new MapEventCreationDebugHook();
        hook.Arm("attacker", "defender");
        var request = new NetworkRequestCreateMapEvent("request", "attacker", "defender", default(BattleCreationFlags));
        Assert.True(hook.TryConsume(request));

        hook.Clear();

        Assert.False(hook.IsArmed);
        Assert.Equal(0, hook.RejectionCount);
    }
}
#endif
