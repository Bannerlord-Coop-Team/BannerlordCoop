using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using Missions.Battles;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleSessionTests
{
    private readonly Mock<IControllerIdProvider> controllerIdProvider = new();
    private readonly BattleHostRegistry hostRegistry;
    private readonly BattleSession session;

    public BattleSessionTests()
    {
        controllerIdProvider.SetupGet(p => p.ControllerId).Returns("us");
        hostRegistry = new BattleHostRegistry(controllerIdProvider.Object);
        session = new BattleSession(controllerIdProvider.Object, hostRegistry);
    }

    [Fact]
    public void TryBegin_SetsInstance_OnlyOnce()
    {
        Assert.False(session.HasInstance);

        Assert.True(session.TryBegin("mapEvent1"));
        Assert.True(session.HasInstance);
        Assert.Equal("mapEvent1", session.InstanceId);

        // OpenBattleMission can fire more than once around an encounter — the second begin must not reconnect.
        Assert.False(session.TryBegin("mapEvent2"));
        Assert.Equal("mapEvent1", session.InstanceId);
    }

    [Fact]
    public void OwnControllerId_TracksTheProvider_NotASnapshot()
    {
        // The controller id can be assigned after the session is constructed (e.g. the E2E harness sets it late).
        controllerIdProvider.SetupGet(p => p.ControllerId).Returns("renamed");
        Assert.Equal("renamed", session.OwnControllerId);
        Assert.True(session.IsOwn("renamed"));
        Assert.False(session.IsOwn("someone-else"));
    }

    [Fact]
    public void IsLocalHost_FalseWithoutInstance_EvenWhenRegistryWouldMatch()
    {
        hostRegistry.Set("mapEvent1", new BattleHostAssignment("us", Array.Empty<string>()));
        Assert.False(session.IsLocalHost);
    }

    [Fact]
    public void IsLocalHost_ReflectsTheHostRegistry()
    {
        session.TryBegin("mapEvent1");
        Assert.False(session.IsLocalHost);

        hostRegistry.Set("mapEvent1", new BattleHostAssignment("us", Array.Empty<string>()));
        Assert.True(session.IsLocalHost);

        hostRegistry.Set("mapEvent1", new BattleHostAssignment("other", new[] { "us" }));
        Assert.False(session.IsLocalHost);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void HostEpoch_IsZeroWithoutAnInstance_EvenWhenTheRegistryHasOne()
    {
        hostRegistry.Set("mapEvent1", new BattleHostAssignment("us", Array.Empty<string>(), epoch: 3));
        Assert.Equal(0, session.HostEpoch);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void HostEpoch_TracksTheCurrentAssignment()
    {
        session.TryBegin("mapEvent1");

        // No assignment received yet: 0 = "cannot judge", the unstamped/accept convention.
        Assert.Equal(0, session.HostEpoch);

        hostRegistry.Set("mapEvent1", new BattleHostAssignment("us", Array.Empty<string>(), epoch: 1));
        Assert.Equal(1, session.HostEpoch);

        // A migration replaces the assignment; the session reflects the new generation immediately.
        hostRegistry.Set("mapEvent1", new BattleHostAssignment("other", new[] { "us" }, epoch: 2));
        Assert.Equal(2, session.HostEpoch);
    }

    [Fact]
    public void IsHostController_MatchesTheRecordedHost()
    {
        session.TryBegin("mapEvent1");
        Assert.False(session.IsHostController("other"));

        hostRegistry.Set("mapEvent1", new BattleHostAssignment("other", Array.Empty<string>()));
        Assert.True(session.IsHostController("other"));
        Assert.False(session.IsHostController("us"));
    }
}
