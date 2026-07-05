using Autofac;
using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobilePartyAIs.Patches;
using Moq;
using System;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

/// <summary>
/// Prevents tests that mutate shared role and dependency-container state from running in parallel.
/// </summary>
[CollectionDefinition(nameof(PartyBehaviorPatchCollection), DisableParallelization = true)]
public class PartyBehaviorPatchCollection
{
}

/// <summary>
/// Tests that client AI calculation does not apply provisional short-term behavior locally.
/// </summary>
[Collection(nameof(PartyBehaviorPatchCollection))]
public class PartyBehaviorPatchTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly bool hadPreviousContainer;
    private readonly ILifetimeScope previousContainer;
    private readonly IContainer testContainer;

    public PartyBehaviorPatchTests()
    {
        hadPreviousContainer = ContainerProvider.TryGetContainer(out previousContainer);

        var syncPolicy = new Mock<ISyncPolicy>();
        syncPolicy.Setup(policy => policy.AllowOriginal()).Returns(false);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(syncPolicy.Object).As<ISyncPolicy>();
        testContainer = builder.Build();
        ContainerProvider.SetContainer(testContainer);
    }

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
        if (hadPreviousContainer)
        {
            ContainerProvider.SetContainer(previousContainer);
        }
        else
        {
            ContainerProvider.Clear();
        }

        testContainer.Dispose();
    }

    [Fact]
    public void SetShortTermBehavior_ClientCalculation_BlocksProvisionalApply()
    {
        ModInformation.IsServer = false;
        PartyBehaviorPatch.GetBehaviorsPrefix(out var state);

        try
        {
            Assert.False(MobilePartyShortTermBehaviorPatches.SetShortTermBehaviorPrefix());
        }
        finally
        {
            PartyBehaviorPatch.GetBehaviorsFinalizer(state);
        }
    }

    [Fact]
    public void SetShortTermBehavior_ClientOutsideCalculation_AllowsApply()
    {
        ModInformation.IsServer = false;

        Assert.True(MobilePartyShortTermBehaviorPatches.SetShortTermBehaviorPrefix());
    }

    [Fact]
    public void SetShortTermBehavior_ServerCalculation_AllowsApply()
    {
        ModInformation.IsServer = true;
        PartyBehaviorPatch.GetBehaviorsPrefix(out var state);

        try
        {
            Assert.True(MobilePartyShortTermBehaviorPatches.SetShortTermBehaviorPrefix());
        }
        finally
        {
            PartyBehaviorPatch.GetBehaviorsFinalizer(state);
        }
    }

    [Fact]
    public void SetShortTermBehavior_AllowedClientCalculation_AllowsApply()
    {
        ModInformation.IsServer = false;
        PartyBehaviorPatch.GetBehaviorsPrefix(out var state);

        try
        {
            using (new AllowedThread())
            {
                Assert.True(MobilePartyShortTermBehaviorPatches.SetShortTermBehaviorPrefix());
            }
        }
        finally
        {
            PartyBehaviorPatch.GetBehaviorsFinalizer(state);
        }
    }

    [Fact]
    public void GetBehaviorsFinalizer_ClearsCalculationScope()
    {
        ModInformation.IsServer = false;
        PartyBehaviorPatch.GetBehaviorsPrefix(out var state);

        PartyBehaviorPatch.GetBehaviorsFinalizer(state);

        Assert.True(MobilePartyShortTermBehaviorPatches.SetShortTermBehaviorPrefix());
    }
}
