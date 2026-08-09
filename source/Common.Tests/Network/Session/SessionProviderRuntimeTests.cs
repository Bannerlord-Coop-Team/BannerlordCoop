using Common.Network.Session;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Common.Tests.Network.Session;

public class SessionProviderRuntimeTests
{
    [Fact]
    public void Dispose_ReleasesComposedServicesOnceInProviderOrder()
    {
        var disposed = new List<string>();
        var advertiser = DisposableMock<ISessionAdvertiser>("advertiser", disposed);
        var tunnelHost = DisposableMock<ISessionTunnelHost>("tunnel", disposed);
        var missionTransport = DisposableMock<IMissionPeerTransport>("mission", disposed);
        var identityPublisher = DisposableMock<IPeerIdentityPublisher>("publisher", disposed);
        var providerResource = new RecordingDisposable("provider", disposed);
        var runtime = new SessionProviderRuntime(
            advertiser.Object,
            tunnelHost.Object,
            Mock.Of<ISessionMembership>(),
            Mock.Of<ISessionAdvertisementOwner>(),
            Mock.Of<ISessionServerReadiness>(),
            Mock.Of<ISessionTransportTargetSource>(),
            missionTransport.Object,
            Mock.Of<IAuthenticatedPeerIdentityResolver>(),
            identityPublisher.Object,
            providerResource);

        runtime.Dispose();
        runtime.Dispose();

        Assert.Equal(
            new[] { "mission", "advertiser", "tunnel", "publisher", "provider" },
            disposed);
    }

    private static Mock<T> DisposableMock<T>(string name, ICollection<string> disposed)
        where T : class, IDisposable
    {
        var mock = new Mock<T>();
        mock.Setup(instance => instance.Dispose()).Callback(() => disposed.Add(name));
        return mock;
    }

    private sealed class RecordingDisposable : IDisposable
    {
        private readonly string name;
        private readonly ICollection<string> disposed;

        public RecordingDisposable(string name, ICollection<string> disposed)
        {
            this.name = name;
            this.disposed = disposed;
        }

        public void Dispose() => disposed.Add(name);
    }
}
