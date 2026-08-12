using GameInterface.Services.Entity;
using GameInterface.Services.Locations.Hosting;
using Missions.Locations;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Missions.Locations;

public class LocationSessionTests
{
    private readonly Mock<IControllerIdProvider> controllerIdProvider = new();
    private readonly LocationHostRegistry hostRegistry;
    private readonly LocationSession session;

    public LocationSessionTests()
    {
        controllerIdProvider.SetupGet(p => p.ControllerId).Returns("us");
        hostRegistry = new LocationHostRegistry(controllerIdProvider.Object);
        session = new LocationSession(controllerIdProvider.Object, hostRegistry);
    }

    [Fact]
    public void TryBegin_SetsInstance_OnlyOnce()
    {
        Assert.False(session.HasInstance);

        Assert.True(session.TryBegin("settlement1|loc_tavern"));
        Assert.True(session.HasInstance);
        Assert.Equal("settlement1|loc_tavern", session.InstanceId);

        // The location entry patches can fire more than once per visit — the second begin must not reconnect.
        Assert.False(session.TryBegin("settlement1|loc_center"));
        Assert.Equal("settlement1|loc_tavern", session.InstanceId);
    }

    [Fact]
    public void OwnControllerId_TracksTheProvider_NotASnapshot()
    {
        controllerIdProvider.SetupGet(p => p.ControllerId).Returns("renamed");
        Assert.Equal("renamed", session.OwnControllerId);
        Assert.True(session.IsOwn("renamed"));
        Assert.False(session.IsOwn("someone-else"));
    }

    [Fact]
    public void IsLocalHost_FalseWithoutInstance_EvenWhenRegistryWouldMatch()
    {
        hostRegistry.Set("settlement1|loc_tavern", new LocationHostAssignment("us", Array.Empty<string>()));
        Assert.False(session.IsLocalHost);
    }

    [Fact]
    public void IsLocalHost_ReflectsTheHostRegistry()
    {
        session.TryBegin("settlement1|loc_tavern");
        Assert.False(session.IsLocalHost);

        hostRegistry.Set("settlement1|loc_tavern", new LocationHostAssignment("us", Array.Empty<string>()));
        Assert.True(session.IsLocalHost);

        hostRegistry.Set("settlement1|loc_tavern", new LocationHostAssignment("other", new[] { "us" }));
        Assert.False(session.IsLocalHost);
    }

    [Fact]
    [Trait("Requirement", "SR-016")]
    public void HostEpoch_IsZeroWithoutAnInstance_EvenWhenTheRegistryHasOne()
    {
        hostRegistry.Set("settlement1|loc_tavern", new LocationHostAssignment("us", Array.Empty<string>(), epoch: 3));
        Assert.Equal(0, session.HostEpoch);
    }

    [Fact]
    [Trait("Requirement", "SR-016")]
    public void HostEpoch_TracksTheCurrentAssignment()
    {
        session.TryBegin("settlement1|loc_tavern");
        Assert.Equal(0, session.HostEpoch);

        hostRegistry.Set("settlement1|loc_tavern", new LocationHostAssignment("us", Array.Empty<string>(), epoch: 1));
        Assert.Equal(1, session.HostEpoch);

        hostRegistry.Set("settlement1|loc_tavern", new LocationHostAssignment("other", new[] { "us" }, epoch: 2));
        Assert.Equal(2, session.HostEpoch);
    }

    [Fact]
    public void IsHostController_MatchesTheRecordedHost()
    {
        session.TryBegin("settlement1|loc_tavern");
        Assert.False(session.IsHostController("other"));

        hostRegistry.Set("settlement1|loc_tavern", new LocationHostAssignment("other", Array.Empty<string>()));
        Assert.True(session.IsHostController("other"));
        Assert.False(session.IsHostController("us"));
    }
}
