using Common.Network.Session;
using Moq;
using System;
using Xunit;

namespace Common.Tests.Network.Session;

/// <summary>Serializes tests that mutate process-wide session discovery state.</summary>
[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class SessionDiscoveryCollection
{
    public const string CollectionName = nameof(SessionDiscoveryCollection);
}

/// <summary>Verifies configured and authenticated provider state remain distinct.</summary>
[Collection(SessionDiscoveryCollection.CollectionName)]
public sealed class SessionDiscoveryTests : IDisposable
{
    public void Dispose()
    {
        SessionDiscovery.ClientProvider = null;
        SessionDiscovery.ServerProvider = null;
    }

    [Fact]
    public void ProviderConfigured_NoClientProvider_ReturnsFalse()
    {
        SessionDiscovery.ClientProvider = null;

        Assert.False(SessionDiscovery.ProviderConfigured);
        Assert.False(SessionDiscovery.ProviderAvailable);
    }

    [Fact]
    public void ProviderConfigured_UnauthenticatedClientProvider_RemainsTrue()
    {
        var provider = new Mock<ISessionProvider>();
        provider.SetupGet(instance => instance.IsAvailable).Returns(false);
        SessionDiscovery.ClientProvider = provider.Object;

        Assert.True(SessionDiscovery.ProviderConfigured);
        Assert.False(SessionDiscovery.ProviderAvailable);
    }

    [Fact]
    public void ProviderAvailable_AuthenticatedClientProvider_ReturnsTrue()
    {
        var provider = new Mock<ISessionProvider>();
        provider.SetupGet(instance => instance.IsAvailable).Returns(true);
        SessionDiscovery.ClientProvider = provider.Object;

        Assert.True(SessionDiscovery.ProviderConfigured);
        Assert.True(SessionDiscovery.ProviderAvailable);
    }
}
