using Autofac;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Hosting;
using GameInterface.Services.Locations.Patches;
using Moq;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

/// <summary>Covers how the location patches read the authority state from the container, and their vanilla fallback without one.</summary>
[Collection(ModInformationRoleCollection.Name)]
public class LocationAuthorityPatchFallbackTests
{
    private const string TavernInstanceId = "town_ES1|Location_town_ES1_tavern";

    /// <summary>Without a container the native spawn entry points run vanilla.</summary>
    [Fact]
    public void NoContainer_AllowsNativeSpawns()
    {
        WithContainer(null, () => Assert.True(LocationNativeSpawnSuppressionPatches.IsNativeSpawnAllowed()));
    }

    /// <summary>Without a container no spawn is captured for NPC replication.</summary>
    [Fact]
    public void NoContainer_DoesNotCaptureSpawns()
    {
        WithContainer(null, () => Assert.False(LocationAgentSpawnedPatch.IsCapturingHost()));
    }

    /// <summary>Without a container the native agent simulation runs vanilla.</summary>
    [Fact]
    public void NoContainer_SimulatesAgents()
    {
        WithContainer(null, () => Assert.True(LocationNativeSpawnSuppressionPatches.SkipPlayerPartySimulation(agent: null)));
    }

    /// <summary>An active mission whose host is not confirmed suppresses native spawns and captures nothing.</summary>
    [Fact]
    public void ActiveMissionWithoutConfirmedHost_SuppressesSpawnsAndDoesNotCapture()
    {
        WithAuthorityState(isHost: false, state =>
        {
            Assert.False(LocationNativeSpawnSuppressionPatches.IsNativeSpawnAllowed());
            Assert.False(LocationAgentSpawnedPatch.IsCapturingHost());
        });
    }

    /// <summary>An active mission with this client as confirmed host allows native spawns and captures them.</summary>
    [Fact]
    public void ActiveMissionWithConfirmedHost_AllowsSpawnsAndCaptures()
    {
        WithAuthorityState(isHost: true, state =>
        {
            Assert.True(LocationNativeSpawnSuppressionPatches.IsNativeSpawnAllowed());
            Assert.True(LocationAgentSpawnedPatch.IsCapturingHost());
        });
    }

    /// <summary>A capture suppression scope keeps the confirmed host from capturing its own spawns.</summary>
    [Fact]
    public void ConfirmedHostInsideSuppressCapture_DoesNotCapture()
    {
        WithAuthorityState(isHost: true, state =>
        {
            using (state.SuppressCapture())
                Assert.False(LocationAgentSpawnedPatch.IsCapturingHost());
        });
    }

    /// <summary>Runs the assertion against a container holding an authority state for an active tavern mission.</summary>
    private static void WithAuthorityState(bool isHost, Action<ILocationNpcAuthorityState> assertion)
    {
        var hostRegistry = new Mock<ILocationHostRegistry>();
        hostRegistry.Setup(registry => registry.IsHost(TavernInstanceId)).Returns(isHost);
        using var state = new LocationNpcAuthorityState(hostRegistry.Object);
        state.BeginMission(TavernInstanceId);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(state).As<ILocationNpcAuthorityState>().ExternallyOwned();
        using IContainer container = builder.Build();

        WithContainer(container, () => assertion(state));
    }

    /// <summary>Runs the assertion with the given container (or none), then restores the previous container.</summary>
    private static void WithContainer(ILifetimeScope container, Action assertion)
    {
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            if (container == null) ContainerProvider.Clear();
            else ContainerProvider.SetContainer(container);

            assertion();
        }
        finally
        {
            if (hadPreviousContainer) ContainerProvider.SetContainer(previousContainer);
            else ContainerProvider.Clear();
        }
    }
}
