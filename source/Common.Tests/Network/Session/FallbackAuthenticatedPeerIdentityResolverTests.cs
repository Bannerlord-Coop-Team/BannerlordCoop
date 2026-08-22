using Common.Network.Session;
using System.Net;
using Xunit;

namespace Common.Tests.Network.Session;

public class FallbackAuthenticatedPeerIdentityResolverTests
{
    [Fact]
    public void TryGetIdentity_PrimaryMatchWinsWithoutConsultingFallback()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 43140);
        var primaryIdentity = new PlatformIdentity("steam", "42");
        var primary = new RecordingResolver(endpoint, primaryIdentity);
        var fallback = new RecordingResolver(endpoint, new PlatformIdentity("gog", "42"));
        var resolver = new FallbackAuthenticatedPeerIdentityResolver(primary, fallback);

        Assert.True(resolver.TryGetIdentity(endpoint, out var identity));
        Assert.Equal(primaryIdentity, identity);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, fallback.CallCount);
    }

    [Fact]
    public void TryGetIdentity_PrimaryMissUsesProviderFallback()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 43141);
        var fallbackIdentity = new PlatformIdentity("gog", "84");
        var primary = new RecordingResolver();
        var fallback = new RecordingResolver(endpoint, fallbackIdentity);
        var resolver = new FallbackAuthenticatedPeerIdentityResolver(primary, fallback);

        Assert.True(resolver.TryGetIdentity(endpoint, out var identity));
        Assert.Equal(fallbackIdentity, identity);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, fallback.CallCount);
    }

    [Fact]
    public void TryGetIdentity_NeitherSourceMatchesReturnsFalse()
    {
        var resolver = new FallbackAuthenticatedPeerIdentityResolver(
            new RecordingResolver(),
            new RecordingResolver());

        Assert.False(resolver.TryGetIdentity(
            new IPEndPoint(IPAddress.Loopback, 43142),
            out var identity));
        Assert.Equal(default, identity);
    }

    private sealed class RecordingResolver : IAuthenticatedPeerIdentityResolver
    {
        private readonly IPEndPoint? endpoint;
        private readonly PlatformIdentity identity;

        public RecordingResolver()
        {
        }

        public RecordingResolver(IPEndPoint endpoint, PlatformIdentity identity)
        {
            this.endpoint = endpoint;
            this.identity = identity;
        }

        public int CallCount { get; private set; }

        public bool TryGetIdentity(IPEndPoint serverPeerEndpoint, out PlatformIdentity resolvedIdentity)
        {
            CallCount++;
            if (endpoint != null && endpoint.Equals(serverPeerEndpoint))
            {
                resolvedIdentity = identity;
                return true;
            }

            resolvedIdentity = default;
            return false;
        }
    }
}
