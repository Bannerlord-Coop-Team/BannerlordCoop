using Common.Network.Session;
using System.Net;
using System.Diagnostics;
using Xunit;

namespace Common.Tests.Network.Session;

public class PeerIdentityBridgeTests
{
    [Fact]
    public void RegisterAndUnregister_BindsExactLoopbackEndpointToProviderIdentity()
    {
        string bridgeName = PeerIdentityBridgeName.Create();
        using var resolver = new NamedPipePeerIdentityResolver(bridgeName);
        using var publisher = new NamedPipePeerIdentityPublisher(bridgeName);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 43127);
        var identity = new PlatformIdentity("gog", "123456789");

        Assert.True(publisher.TryRegister(endpoint, identity));
        Assert.True(resolver.TryGetIdentity(endpoint, out var resolved));
        Assert.Equal(identity, resolved);
        Assert.False(resolver.TryGetIdentity(
            new IPEndPoint(IPAddress.Loopback, endpoint.Port + 1),
            out _));

        publisher.Unregister(endpoint);

        Assert.False(resolver.TryGetIdentity(endpoint, out _));
    }

    [Fact]
    public void Register_SameNumericIdKeepsStorefrontNamespaceAuthoritative()
    {
        string bridgeName = PeerIdentityBridgeName.Create();
        using var resolver = new NamedPipePeerIdentityResolver(bridgeName);
        using var publisher = new NamedPipePeerIdentityPublisher(bridgeName);
        var steamEndpoint = new IPEndPoint(IPAddress.Loopback, 43128);
        var gogEndpoint = new IPEndPoint(IPAddress.Loopback, 43129);

        Assert.True(publisher.TryRegister(
            steamEndpoint,
            new PlatformIdentity("steam", "42")));
        Assert.True(publisher.TryRegister(
            gogEndpoint,
            new PlatformIdentity("gog", "42")));

        Assert.True(resolver.TryGetIdentity(steamEndpoint, out var steamIdentity));
        Assert.True(resolver.TryGetIdentity(gogEndpoint, out var gogIdentity));
        Assert.Equal("steam:42", steamIdentity.ControllerId);
        Assert.Equal("gog:42", gogIdentity.ControllerId);
        Assert.NotEqual(steamIdentity, gogIdentity);
    }

    [Fact]
    public void UnregisterAll_ClearsEveryEndpointInOneBridgeRequest()
    {
        string bridgeName = PeerIdentityBridgeName.Create();
        using var resolver = new NamedPipePeerIdentityResolver(bridgeName);
        using var publisher = new NamedPipePeerIdentityPublisher(bridgeName);
        var firstEndpoint = new IPEndPoint(IPAddress.Loopback, 43134);
        var secondEndpoint = new IPEndPoint(IPAddress.Loopback, 43135);
        Assert.True(publisher.TryRegister(firstEndpoint, new PlatformIdentity("gog", "1")));
        Assert.True(publisher.TryRegister(secondEndpoint, new PlatformIdentity("gog", "2")));

        publisher.UnregisterAll();

        Assert.False(resolver.TryGetIdentity(firstEndpoint, out _));
        Assert.False(resolver.TryGetIdentity(secondEndpoint, out _));
    }

    [Fact]
    public void SequentialRequests_ReuseListenerWithoutLosingAcknowledgements()
    {
        string bridgeName = PeerIdentityBridgeName.Create();
        using var resolver = new NamedPipePeerIdentityResolver(bridgeName);
        using var publisher = new NamedPipePeerIdentityPublisher(bridgeName);

        for (int index = 0; index < 100; index++)
        {
            var firstEndpoint = new IPEndPoint(IPAddress.Loopback, 43200 + (index * 2));
            var secondEndpoint = new IPEndPoint(IPAddress.Loopback, firstEndpoint.Port + 1);

            Assert.True(publisher.TryRegister(
                firstEndpoint,
                new PlatformIdentity("steam", index.ToString())));
            Assert.True(publisher.TryRegister(
                secondEndpoint,
                new PlatformIdentity("gog", index.ToString())));

            publisher.UnregisterAll();

            Assert.False(resolver.TryGetIdentity(firstEndpoint, out _));
            Assert.False(resolver.TryGetIdentity(secondEndpoint, out _));
        }
    }

    [Fact]
    public void Register_RejectsNonStorefrontAndNonLoopbackClaims()
    {
        string bridgeName = PeerIdentityBridgeName.Create();
        using var resolver = new NamedPipePeerIdentityResolver(bridgeName);
        using var publisher = new NamedPipePeerIdentityPublisher(bridgeName);

        Assert.False(publisher.TryRegister(
            new IPEndPoint(IPAddress.Loopback, 43130),
            new PlatformIdentity("local", "installation-id")));
        Assert.False(publisher.TryRegister(
            new IPEndPoint(IPAddress.Parse("192.0.2.10"), 43130),
            new PlatformIdentity("gog", "42")));
        Assert.False(publisher.TryRegister(
            new IPEndPoint(IPAddress.IPv6Loopback, 43130),
            new PlatformIdentity("gog", "42")));
    }

    [Fact]
    public void Dispose_WhenBridgeIsUnavailable_BoundsCleanupAcrossAllRegistrations()
    {
        string bridgeName = PeerIdentityBridgeName.Create();
        var resolver = new NamedPipePeerIdentityResolver(bridgeName);
        var publisher = new NamedPipePeerIdentityPublisher(
            bridgeName,
            connectTimeoutMilliseconds: 2000,
            disposeTimeoutMilliseconds: 50);
        Assert.True(publisher.TryRegister(
            new IPEndPoint(IPAddress.Loopback, 43131),
            new PlatformIdentity("gog", "1")));
        Assert.True(publisher.TryRegister(
            new IPEndPoint(IPAddress.Loopback, 43132),
            new PlatformIdentity("gog", "2")));
        Assert.True(publisher.TryRegister(
            new IPEndPoint(IPAddress.Loopback, 43133),
            new PlatformIdentity("gog", "3")));
        resolver.Dispose();

        var stopwatch = Stopwatch.StartNew();
        publisher.Dispose();
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < System.TimeSpan.FromSeconds(1));
        Assert.False(publisher.IsAvailable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bannerlordcoop-peer-identity-not-a-guid")]
    [InlineData("other-00000000000000000000000000000000")]
    public void InvalidBridgeName_IsRejected(string bridgeName)
    {
        Assert.False(PeerIdentityBridgeName.IsValid(bridgeName));
        Assert.ThrowsAny<System.ArgumentException>(() =>
            new NamedPipePeerIdentityPublisher(bridgeName));
        Assert.ThrowsAny<System.ArgumentException>(() =>
            new NamedPipePeerIdentityResolver(bridgeName));
    }
}
