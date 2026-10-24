using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Hosting;
using System;
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

    /// <summary>Builds the state over a mocked host registry.</summary>
    public LocationNpcAuthorityStateTests()
    {
        state = new LocationNpcAuthorityState(hostRegistry.Object);
    }

    /// <summary>Releases the state's per-thread flags.</summary>
    public void Dispose() => state.Dispose();

    /// <summary>With no active mission nothing is confirmed or suppressed, even if the registry names this client.</summary>
    [Fact]
    public void Inactive_NothingSuppressed()
    {
        hostRegistry.Setup(registry => registry.IsHost(It.IsAny<string>())).Returns(true);

        Assert.False(state.IsCoopLocationMissionActive);
        Assert.Null(state.ActiveInstanceId);
        Assert.False(state.IsLocalHostConfirmed);
        Assert.False(state.ShouldSuppressNativeSpawns);
    }

    /// <summary>A started mission suppresses native spawns until the registry confirms this client as host.</summary>
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

    /// <summary>Confirmation follows the registry as the host migrates to and away from this client.</summary>
    [Fact]
    [Trait("Requirement", "SR-011")]
    [Trait("Requirement", "SR-012")]
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

    /// <summary>A real registry assignment naming this client as host confirms it.</summary>
    [Fact]
    public void StoredAssignmentNamingThisClient_ConfirmsHost()
    {
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        controllerIdProvider.SetupGet(provider => provider.ControllerId).Returns("local");
        var registry = new LocationHostRegistry(controllerIdProvider.Object);
        using var registryState = new LocationNpcAuthorityState(registry);
        registryState.BeginMission(TavernInstanceId);

        registry.Set(TavernInstanceId, new LocationHostAssignment("remote", new[] { "local" }, epoch: 1));
        Assert.True(registryState.ShouldSuppressNativeSpawns);

        registry.Set(TavernInstanceId, new LocationHostAssignment("local", Array.Empty<string>(), epoch: 2));
        Assert.True(registryState.IsLocalHostConfirmed);
        Assert.False(registryState.ShouldSuppressNativeSpawns);
    }

    /// <summary>Ending the mission clears the active instance and its confirmation.</summary>
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

    /// <summary>Switching instances reads confirmation for the new instance, not the previous one.</summary>
    [Fact]
    public void BeginMission_ReadsHostConfirmationForTheNewInstance()
    {
        hostRegistry.Setup(registry => registry.IsHost(TavernInstanceId)).Returns(true);
        state.BeginMission(TavernInstanceId);

        state.BeginMission(CenterInstanceId);

        Assert.False(state.IsLocalHostConfirmed);
        Assert.True(state.ShouldSuppressNativeSpawns);
    }

    /// <summary>Disposing a nested capture scope restores the outer scope.</summary>
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

    /// <summary>Disposing a nested replay scope restores the outer scope.</summary>
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
