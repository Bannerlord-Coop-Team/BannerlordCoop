using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Hosting;
using GameInterface.Services.Locations.Patches;
using System;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.MountAndBlade;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.Locations;

public class LocationCompanionAuthorityPatchTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SpawnCapture_ExcludesPlayerOwnedPartyAgents(
        bool isLocalPlayerPartyAgent,
        bool expected)
    {
        Assert.Equal(expected,
            LocationAgentSpawnedPatch.ShouldCaptureAsAmbientNpc(isLocalPlayerPartyAgent));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void DefaultLocationSpawn_BypassesNativeSpawnSuppressionOnlyForOwnedCompanions(
        bool isOwnedCompanion,
        bool suppressNativeSpawns,
        bool expected)
    {
        Assert.Equal(expected,
            LocationNativeSpawnSuppressionPatches.ShouldAllowDefaultLocationSpawn(
                isOwnedCompanion,
                suppressNativeSpawns));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void EnteringLocationSpawn_BypassesNativeSpawnSuppressionOnlyForOwnedCompanions(
        bool isOwnedCompanion,
        bool suppressNativeSpawns,
        bool expected)
    {
        Assert.Equal(expected,
            LocationNativeSpawnSuppressionPatches.ShouldAllowEnteringLocationSpawn(
                isOwnedCompanion,
                suppressNativeSpawns));
    }

    [Fact]
    public void DelayedPopulationSimulation_SkipsMainAgentRegardlessOfOrigin()
    {
        var mainAgent = CreateAgentWithSimpleOrigin();

        using var state = CreateAuthorityState();

        Assert.True(state.IsPlayerPartyAgent(mainAgent, mainAgent));
    }

    [Fact]
    public void DelayedPopulationSimulation_SkipsRegisteredRemotePartyPuppet()
    {
        var remotePartyPuppet = CreateAgentWithSimpleOrigin();
        var ambientNpc = CreateAgentWithSimpleOrigin();
        using var state = CreateAuthorityState();
        state.BeginMission(
            "settlement|tavern",
            agent => ReferenceEquals(agent, remotePartyPuppet));

        using (state.ReplayNativePopulation())
        {
            Assert.False(LocationNativeSpawnSuppressionPatches.ShouldSimulateAgent(state, remotePartyPuppet, mainAgent: null));
            Assert.True(LocationNativeSpawnSuppressionPatches.ShouldSimulateAgent(state, ambientNpc, mainAgent: null));
        }

        Assert.True(LocationNativeSpawnSuppressionPatches.ShouldSimulateAgent(state, remotePartyPuppet, mainAgent: null));
    }

    /// <summary>Builds an authority state over a real, empty host registry.</summary>
    private static LocationNpcAuthorityState CreateAuthorityState()
        => new LocationNpcAuthorityState(new LocationHostRegistry(new ControllerIdProvider()));

    private static Agent CreateAgentWithSimpleOrigin()
    {
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        agent.Origin = (SimpleAgentOrigin)FormatterServices.GetUninitializedObject(typeof(SimpleAgentOrigin));
        return agent;
    }
}
