#if DEBUG
using Missions.Battles;
using Missions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabDeckLocomotionRoutingTests : NavalMissionTestEnvironment
{
    public NavalLabDeckLocomotionRoutingTests(ITestOutputHelper output) : base(output) { }

    private void StartNative(NavalLabMode mode = NavalLabMode.TwoClientNative)
    {
        CreateLab(mode);
        Ready(First); Ready(Second); Tick(First); Tick(Second);
    }

    [Theory]
    [InlineData("walk", 0, 1f)]
    [InlineData("turn", 1, -0.5f)]
    public void TwoClientNativeWalkTurn_ReachOnlyOriginalOwnerAfterNativeRelease(string kind, int ship, float value)
    {
        StartNative();
        Assert.Throws<InvalidOperationException>(() => Execute(kind, ship, value));
        Execute("complete-deployment"); Tick(First); Tick(Second);
        var owner = ship == 0 ? First : Second;
        var other = ship == 0 ? Second : First;

        Guid operation = Execute(kind, ship, value);

        Assert.Equal("applied", Receipt(owner, operation));
        Assert.Equal((kind, ship, value), Assert.Single(Adapter(owner).AgentControlCalls));
        Assert.Empty(Adapter(other).AgentControlCalls);
        Assert.Single(Actions(owner), action => action.OperationId == operation
            && action.DeadlineUtcTicks > DateTime.UtcNow.Ticks && action.DeadlineUtcTicks <= DateTime.UtcNow.AddSeconds(1).Ticks);
    }

    [Theory]
    [InlineData("jump", NavalLabMode.TwoClientNative)]
    [InlineData("crew", NavalLabMode.TwoClientNative)]
    [InlineData("walk", NavalLabMode.TwoClientNativeAllPhysics)]
    public void OtherScriptedAgentControls_RemainKeyboardOnly(string kind, NavalLabMode mode)
    {
        StartNative(mode);

        Assert.Throws<InvalidOperationException>(() => Execute(kind));
        Assert.All(Clients, client => Assert.Empty(Adapter(client).AgentControlCalls));
    }

    [Fact]
    public void ExpiredOrForeignWalk_IsRejectedAtRecipient()
    {
        StartNative();
        Execute("complete-deployment"); Tick(First); Tick(Second);
        var expired = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, "walk", 0, 1, false, 0);
        var foreign = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, "walk", 1, 1, false,
            DateTime.UtcNow.AddSeconds(1).Ticks);

        SendAction(First, expired);
        SendAction(First, foreign);

        Assert.Equal("rejected:expired_control", Receipt(First, expired.OperationId));
        Assert.Equal("rejected:owner_not_ready", Receipt(First, foreign.OperationId));
        Assert.Empty(Adapter(First).AgentControlCalls);
    }
}
#endif
