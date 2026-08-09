using Common.Network.Session;
using System;

namespace Coop.Core.Common.Session;

/// <summary>Direct-IP runtime used when no storefront provider is active.</summary>
public sealed class DirectSessionProviderRuntime : ISessionProviderRuntime
{
    private sealed class DirectSessionState :
        ISessionMembership,
        ISessionAdvertisementOwner,
        ISessionServerReadiness,
        ISessionTransportTargetSource
    {
        public bool IsInSession => false;
        public SessionListingId ListingId => default;
        public bool IsReady => false;
        public PlatformIdentity TunnelTarget => default;
        public string PublicAddress => string.Empty;

        public event Action<SessionListingId> ListingChanged
        {
            add { }
            remove { }
        }

        public event Action Ready
        {
            add { }
            remove { }
        }

        public void JoinSession(SessionListingId listingId) { }
        public void LeaveSession() { }
    }

    private readonly DirectSessionState state = new DirectSessionState();
    private readonly IDisposable peerIdentityResolverLifetime;

    public DirectSessionProviderRuntime(bool isServer = false, string peerIdentityBridgeName = null)
    {
        Advertiser = new NoopSessionAdvertiser();
        TunnelHost = new NoopSessionTunnelHost();
        MissionTransport = new NoopMissionPeerTransport();
        PeerIdentityPublisher = NoopPeerIdentityPublisher.Instance;

        if (isServer && PeerIdentityBridgeName.IsValid(peerIdentityBridgeName))
        {
            var bridgeResolver = new NamedPipePeerIdentityResolver(peerIdentityBridgeName);
            PeerIdentityResolver = bridgeResolver;
            peerIdentityResolverLifetime = bridgeResolver;
        }
        else
        {
            PeerIdentityResolver = (IAuthenticatedPeerIdentityResolver)TunnelHost;
        }
    }

    public ISessionAdvertiser Advertiser { get; }
    public ISessionTunnelHost TunnelHost { get; }
    public ISessionMembership Membership => state;
    public ISessionAdvertisementOwner AdvertisementOwner => state;
    public ISessionServerReadiness ServerReadiness => state;
    public ISessionTransportTargetSource TransportTargetSource => state;
    public IMissionPeerTransport MissionTransport { get; }
    public IAuthenticatedPeerIdentityResolver PeerIdentityResolver { get; }
    public IPeerIdentityPublisher PeerIdentityPublisher { get; }

    public void Dispose()
    {
        MissionTransport.Dispose();
        TunnelHost.Dispose();
        Advertiser.Dispose();
        peerIdentityResolverLifetime?.Dispose();
    }
}
