using Autofac;
using GameInterface.Services.MobileParties.Patches.Disable;
using System;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Covers the food consumption prefixes falling back to vanilla when no coop container is active.
/// </summary>
[Collection(ContainerProviderCollection.Name)]
public class FoodConsumptionBehaviorPatchesTests : IDisposable
{
    private readonly ILifetimeScope previousContainer;

    /// <summary>
    /// Clears the container so every test runs as a single-player load after a coop session.
    /// </summary>
    public FoodConsumptionBehaviorPatchesTests()
    {
        ContainerProvider.TryGetContainer(out previousContainer);
        ContainerProvider.Clear();
    }

    /// <summary>
    /// Restores the container the test replaced.
    /// </summary>
    public void Dispose()
    {
        if (previousContainer != null) ContainerProvider.SetContainer(previousContainer);
    }

    /// <summary>
    /// Vanilla daily ticks call these, so parties only eat and breed animals when they let vanilla run.
    /// </summary>
    [Fact]
    public void ConsumptionPrefixes_NoContainer_RunVanilla()
    {
        Assert.True(FoodConsumptionBehaviorPatches.PartyConsumeFoodPrefix(null, null));
        Assert.True(FoodConsumptionBehaviorPatches.CheckAnimalBreedingPrefix(null, null));
    }

    /// <summary>
    /// Army attachment and the starvation tick stay vanilla without a container too.
    /// </summary>
    [Fact]
    public void AttachAndTickPrefixes_NoContainer_RunVanilla()
    {
        Assert.True(FoodConsumptionBehaviorPatches.OnPartyAttachedPartyPrefix(null, null));
        Assert.True(FoodConsumptionBehaviorPatches.OnTickPrefix(null, 0f));
    }
}
