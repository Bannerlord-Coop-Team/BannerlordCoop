using Autofac;
using GameInterface.Services.Heroes.Patches.Disable;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

/// <summary>
/// Covers the aging prefixes falling back to vanilla when no coop container is active.
/// </summary>
[Collection(ContainerProviderCollection.Name)]
public class AgingCampaignBehaviorPatchesTests : IDisposable
{
    private readonly ILifetimeScope previousContainer;

    /// <summary>
    /// Clears the container so every test runs as a single-player load after a coop session.
    /// </summary>
    public AgingCampaignBehaviorPatchesTests()
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
    /// Heroes keep aging and coming of age in vanilla when the aging service doesnt resolve.
    /// </summary>
    [Fact]
    public void AgingPrefixes_NoContainer_RunVanilla()
    {
        Assert.True(AgingCampaignBehaviorPatches.DailyTickHeroPrefix(null, null));
        Assert.True(AgingCampaignBehaviorPatches.OnHeroComesOfAgePrefix(null, null));
    }
}
