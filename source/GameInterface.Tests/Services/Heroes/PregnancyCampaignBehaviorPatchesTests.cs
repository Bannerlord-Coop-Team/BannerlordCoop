using Autofac;
using GameInterface.Services.Heroes.Patches.Disable;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

/// <summary>
/// Covers the pregnancy prefixes falling back to vanilla when no coop container is active.
/// </summary>
[Collection(ContainerProviderCollection.Name)]
public class PregnancyCampaignBehaviorPatchesTests : IDisposable
{
    private readonly ILifetimeScope previousContainer;

    /// <summary>
    /// Clears the container so every test runs as a single-player load after a coop session.
    /// </summary>
    public PregnancyCampaignBehaviorPatchesTests()
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
    /// Births and the spouse proximity check stay vanilla when the pregnancy service doesnt resolve.
    /// </summary>
    [Fact]
    public void PregnancyPrefixes_NoContainer_RunVanilla()
    {
        bool areNearby = false;

        Assert.True(PregnancyCampaignBehaviorPatches.CheckOffspringToDeliverPrefix(null, null));
        Assert.True(PregnancyCampaignBehaviorPatches.CheckAreNearbyPrefix(null, ref areNearby, null, null));
    }
}
