using GameInterface.Services.Locations.Patches;
using Xunit;

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
}
