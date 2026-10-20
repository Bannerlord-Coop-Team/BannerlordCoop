using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Hosting;
using System;
using System.Threading;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Locations;

/// <summary>Covers the active location mission tracking and its registry-backed NPC host confirmation.</summary>
public class LocationNpcAuthorityStateTests : IDisposable
{
    private const string TavernInstanceId = "settlement1|loc_tavern";
    private const string CenterInstanceId = "settlement1|loc_center";

    private readonly Mock<ILocationHostRegistry> hostRegistry = new Mock<ILocationHostRegistry>();
    private readonly LocationNpcAuthorityState state;

    public LocationNpcAuthorityStateTests()
    {
        state = new LocationNpcAuthorityState(hostRegistry.Object);
    }

    public void Dispose() => state.Dispose();

    [Fact]
    public void Inactive_NothingSuppressed()
    {
        hostRegistry.Setup(registry => registry.IsHost(It.IsAny<string>())).Returns(true);

        Assert.False(state.IsCoopLocationMissionActive);
        Assert.Null(state.ActiveInstanceId);
        Assert.False(state.IsLocalHostConfirmed);
        Assert.False(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-013")]
    public void BeginMission_SuppressesUntilHostConfirmed()
    {
        state.BeginMission(TavernInstanceId);

        Assert.True(state.IsCoopLocationMissionActive);
        Assert.Equal(TavernInstanceId, state.ActiveInstanceId);
        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-011")]
    public void ConfirmedHost_LiftsSuppression()
    {
        hostRegistry.Setup(registry => registry.IsHost(TavernInstanceId)).Returns(true);
        state.BeginMission(TavernInstanceId);

        Assert.True(state.IsLocalHostConfirmed);
        Assert.False(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-012")]
    public void ConfirmedNonHost_StaysSuppressed()
    {
        hostRegistry.Setup(registry => registry.IsHost(TavernInstanceId)).Returns(false);
        state.BeginMission(TavernInstanceId);

        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void HostOfAnotherInstance_DoesNotConfirmActiveInstance()
    {
        hostRegistry.Setup(registry => registry.IsHost(CenterInstanceId)).Returns(true);
        state.BeginMission(TavernInstanceId);

        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    [Trait("Requirement", "SR-014")]
    public void HostMigration_FollowsTheRegistry()
    {
        bool isHost = false;
        hostRegistry.Setup(registry => registry.IsHost(TavernInstanceId)).Returns(() => isHost);
        state.BeginMission(TavernInstanceId);
        Assert.True(state.ShouldSuppressNativeSpawns);

        isHost = true;
        Assert.True(state.IsLocalHostConfirmed);
        Assert.False(state.ShouldSuppressNativeSpawns);

        isHost = false;
        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void StoredAssignmentNamingThisClient_ConfirmsHost()
    {
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        controllerIdProvider.SetupGet(provider => provider.ControllerId).Returns("local");
        var registry = new LocationHostRegistry(controllerIdProvider.Object);
        var registryState = new LocationNpcAuthorityState(registry);
        registryState.BeginMission(TavernInstanceId);

        registry.Set(TavernInstanceId, new LocationHostAssignment("remote", new[] { "local" }, epoch: 1));
        Assert.True(registryState.ShouldSuppressNativeSpawns);

        registry.Set(TavernInstanceId, new LocationHostAssignment("local", Array.Empty<string>(), epoch: 2));
        Assert.True(registryState.IsLocalHostConfirmed);
        Assert.False(registryState.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void EndMission_ResetsEverything()
    {
        hostRegistry.Setup(registry => registry.IsHost(TavernInstanceId)).Returns(true);
        state.BeginMission(TavernInstanceId);

        state.EndMission();

        Assert.False(state.IsCoopLocationMissionActive);
        Assert.Null(state.ActiveInstanceId);
        Assert.False(state.IsLocalHostConfirmed);
        Assert.False(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void BeginMission_ReadsHostConfirmationForTheNewInstance()
    {
        hostRegistry.Setup(registry => registry.IsHost(TavernInstanceId)).Returns(true);
        state.BeginMission(TavernInstanceId);

        state.BeginMission(CenterInstanceId);

        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
    }

    [Fact]
    public void SuppressCapture_IsRaisedOnlyInsideTheScope()
    {
        Assert.False(state.IsCaptureSuppressed);

        using (state.SuppressCapture())
            Assert.True(state.IsCaptureSuppressed);

        Assert.False(state.IsCaptureSuppressed);
    }

    [Fact]
    public void SuppressCapture_NestedScopeRestoresTheOuterScope()
    {
        using (state.SuppressCapture())
        {
            using (state.SuppressCapture())
                Assert.True(state.IsCaptureSuppressed);

            Assert.True(state.IsCaptureSuppressed);
        }

        Assert.False(state.IsCaptureSuppressed);
    }

    [Fact]
    public void SuppressCapture_DisposingTwiceDoesNotClearAnOuterScope()
    {
        using (state.SuppressCapture())
        {
            IDisposable inner = state.SuppressCapture();
            inner.Dispose();
            inner.Dispose();

            Assert.True(state.IsCaptureSuppressed);
        }
    }

    [Fact]
    public void SuppressCapture_IsPerThread()
    {
        bool otherThreadSuppressed = true;

        using (state.SuppressCapture())
        {
            var thread = new Thread(() => otherThreadSuppressed = state.IsCaptureSuppressed);
            thread.Start();
            thread.Join();
        }

        Assert.False(otherThreadSuppressed);
    }

    [Fact]
    public void ReplayNativePopulation_NestedScopeRestoresTheOuterScope()
    {
        using (state.ReplayNativePopulation())
        {
            using (state.ReplayNativePopulation())
                Assert.True(state.IsReplayingNativePopulation);

            Assert.True(state.IsReplayingNativePopulation);
        }

        Assert.False(state.IsReplayingNativePopulation);
    }
}
