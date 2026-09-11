#if DEBUG
using Common.Commands;
using Missions.Battles;
using Missions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabSailFeedbackTests : NavalMissionTestEnvironment
{
    public NavalLabSailFeedbackTests(ITestOutputHelper output) : base(output) { }
    private void Start(bool secondFirst = false)
    {
        CreateLab(NavalLabMode.TwoClientNative);
        Ready(secondFirst ? Second : First); Ready(secondFirst ? First : Second);
        Tick(First); Tick(Second); Execute("complete-deployment"); Tick(First); Tick(Second);
        Adapter(First).SailFeedback.Clear(); Adapter(Second).SailFeedback.Clear();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ElectedHostObservationsTravelWithExactFrameIdentity_OnlyFollowerReceives(bool secondFirst)
    {
        Start(secondFirst);
        var host = secondFirst ? Second : First; var follower = secondFirst ? First : Second;
        Adapter(host).SailStates = new[] { new NetworkNavalLabSailState(Manifest.Ships[0], 0, 0), new NetworkNavalLabSailState(Manifest.Ships[1], 2, 0) };
        Tick(host);
        var feedback = Assert.Single(Adapter(follower).SailFeedback);
        Assert.Equal(Manifest.IncarnationId, feedback.IncarnationId); Assert.Equal(1, feedback.Epoch);
        Assert.True(feedback.Sequence > 0); Assert.True(feedback.SailDeadlineUtcTicks > DateTime.UtcNow.Ticks);
        Assert.Equal(Manifest.Ships, feedback.SailStates.Select(state => state.ShipId));
        Assert.Equal(new[] { 0, 2 }, feedback.SailStates.Select(state => state.State));
        Assert.Empty(Adapter(host).SailFeedback); Assert.Empty(Adapter(follower).NativeInputs);
        Assert.Empty(Adapter(follower).HelmCalls);
    }

    [Theory]
    [InlineData("epoch")]
    [InlineData("incarnation")]
    [InlineData("old_sequence")]
    [InlineData("terminal")]
    public void RejectedFrameCannotRefreshPresentation(string condition)
    {
        Start(); Tick(First); Adapter(Second).SailFeedback.Clear();
        if (condition == "terminal") Execute("stop");
        var frame = new NetworkNavalLabFrames(condition == "incarnation" ? Guid.NewGuid() : Manifest.IncarnationId,
            condition == "epoch" ? 2 : 1, condition == "old_sequence" ? 1 : 100, new float[24], 100,
            sailStates: new[] { new NetworkNavalLabSailState(Manifest.Ships[0], 2, 0), new NetworkNavalLabSailState(Manifest.Ships[1], 2, 0) },
            sailDeadlineUtcTicks: DateTime.UtcNow.AddSeconds(1).Ticks);
        SendFrames(First, frame); Assert.Empty(Adapter(Second).SailFeedback);
        Assert.Empty(Adapter(Second).NativeInputs);
    }

    [Fact]
    public void FramesBeforeNativeReadinessDoNotApplyFeedback()
    {
        CreateLab(NavalLabMode.TwoClientNative); Ready(First); Ready(Second); Tick(First); Tick(Second);
        Tick(First); Assert.Empty(Adapter(Second).SailFeedback); Assert.True(Adapter(Second).SailClears > 0);
    }

    private CoopCommandResult Command(E2E.Tests.Environment.Instance.EnvironmentInstance instance, string name, params string[] values)
    {
        CoopCommandResult result = null!;
        instance.Call(() => result = instance.Resolve<ICoopCommandRegistry>().ProcessCommand("coop.debug.naval_lab." + name,
            new CoopCommandArgsFactory().FromValues(values)));
        PumpAll(); return result;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteredCommandsKeepMutationOnServerAndRouteSyntheticSailToOriginalOwner(bool secondFirst)
    {
        Start(secondFirst);
        string operation = Guid.NewGuid().ToString();
        Assert.False(Command(Second, "action", operation, "sail-full", "1", "0", "false").Succeeded);
        Assert.True(Command(Server, "action", operation, "sail-full", "1", "0", "false").Succeeded);
        Assert.Equal(2, Assert.Single(Adapter(Second).SailRequests)); Assert.Empty(Adapter(First).SailRequests);
        Assert.True(Command(Server, "action", operation, "sail-full", "1", "0", "false").Succeeded);
        Assert.Single(Adapter(Second).SailRequests);
        Assert.StartsWith("requested:", Receipt(Second, Guid.Parse(operation)));
        var status = Command(Second, "sail-status"); Assert.True(status.Succeeded);
        Assert.Contains("simulated", status.Output);
        Assert.False(Command(Second, "sail-status", "extra").Succeeded);
        Assert.False(Command(Server, "action", "not-a-uuid", "sail-full", "1", "0", "false").Succeeded);
        Assert.False(Command(Server, "action", Guid.NewGuid().ToString(), "sail-full", "1", "1", "false").Succeeded);
        Assert.False(Command(Server, "action", Guid.NewGuid().ToString(), "sail-full", "2", "0", "false").Succeeded);
        Assert.Empty(Adapter(First).NativeInputs); Assert.Empty(Adapter(Second).NativeInputs);
    }

    [Fact]
    public void SailCommandsRejectOtherModes()
    {
        StartReleased();
        Assert.False(Command(Server, "action", Guid.NewGuid().ToString(), "sail-raised", "0", "0", "false").Succeeded);
        Assert.Contains("wrong_mode", Command(First, "sail-status").Output);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong_owner")]
    [InlineData("not_ready")]
    public void SyntheticInputRejectsStaleOrUnreadyOwnerWithoutCallingNativeBoundary(string reason)
    {
        Start();
        if (reason == "not_ready") Execute("stop");
        var action = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, "sail-raised",
            reason == "wrong_owner" ? 0 : 1, 0, false,
            reason == "expired" ? DateTime.UtcNow.AddSeconds(-1).Ticks : DateTime.UtcNow.AddSeconds(1).Ticks);
        SendAction(Second, action); Assert.Empty(Adapter(Second).SailRequests);
    }
}
#endif
