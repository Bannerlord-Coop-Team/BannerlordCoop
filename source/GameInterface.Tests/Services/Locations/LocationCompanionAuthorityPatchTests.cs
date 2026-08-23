using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Patches;
using System;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.MountAndBlade;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.Locations;

public class LocationCompanionAuthorityPatchTests
{
    [Fact]
    public void AccompanyingCompanions_SpawnOnOwningClient()
    {
        Assert.True(LocationCharacterGuardPatches.ShouldSpawnAccompanyingCharacters(isClient: true));
    }

    [Fact]
    public void AccompanyingCompanions_DoNotSpawnOnDedicatedServer()
    {
        Assert.False(LocationCharacterGuardPatches.ShouldSpawnAccompanyingCharacters(isClient: false));
    }

    [Theory]
    [InlineData(true, false, false, false, 30f, 18f, true)]
    [InlineData(false, false, false, false, 30f, 18f, false)]
    [InlineData(true, true, false, false, 30f, 18f, false)]
    [InlineData(true, false, true, false, 30f, 18f, false)]
    [InlineData(true, false, false, true, 30f, 18f, false)]
    [InlineData(true, false, false, false, 17f, 18f, false)]
    public void VanillaFallback_UsesVanillaBodyguardEligibility(
        bool isHero,
        bool isMainHero,
        bool isPrisoner,
        bool isWounded,
        float age,
        float heroComesOfAge,
        bool expected)
    {
        Assert.Equal(expected,
            LocationCharacterGuardPatches.IsEligibleVanillaCompanion(
                isHero, isMainHero, isPrisoner, isWounded, age, heroComesOfAge));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void VanillaFallback_RetainsOnlyEligibleMainPartyCompanion(
        bool isInMainParty,
        bool isEligible,
        bool expected)
    {
        Assert.Equal(expected,
            LocationCharacterGuardPatches.ShouldRetainExistingCompanion(
                isInMainParty,
                isEligible));
    }

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
    public void DefaultLocationSpawn_BypassesNpcGateOnlyForOwnedCompanions(
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
    public void EnteringLocationSpawn_BypassesNpcGateOnlyForOwnedCompanions(
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

        Assert.True(LocationNpcGate.IsPlayerPartyAgent(mainAgent, mainAgent));
    }

    [Fact]
    public void DelayedPopulationSimulation_SkipsRegisteredRemotePartyPuppet()
    {
        var remotePartyPuppet = CreateAgentWithSimpleOrigin();
        var ambientNpc = CreateAgentWithSimpleOrigin();
        LocationNpcGate.BeginMission(
            "settlement|tavern",
            agent => ReferenceEquals(agent, remotePartyPuppet));

        try
        {
            Assert.False(LocationNativeSpawnSuppressionPatches.ShouldSimulateAgent(
                isReplayingNativePopulation: true,
                remotePartyPuppet));
            Assert.True(LocationNativeSpawnSuppressionPatches.ShouldSimulateAgent(
                isReplayingNativePopulation: true,
                ambientNpc));
            Assert.True(LocationNativeSpawnSuppressionPatches.ShouldSimulateAgent(
                isReplayingNativePopulation: false,
                remotePartyPuppet));
        }
        finally
        {
            LocationNpcGate.EndMission();
        }
    }

    private static Agent CreateAgentWithSimpleOrigin()
    {
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        agent.Origin = (SimpleAgentOrigin)FormatterServices.GetUninitializedObject(typeof(SimpleAgentOrigin));
        return agent;
    }
}
