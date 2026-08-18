using System;

namespace Common.Network.Session;

/// <summary>Owns one composed provider runtime and its provider-specific resources.</summary>
public sealed class SessionProviderRuntime : ISessionProviderRuntime
{
    private readonly IDisposable[] providerResources;
    private bool disposed;

    public SessionProviderRuntime(
        ISessionAdvertiser advertiser,
        ISessionTunnelHost tunnelHost,
        ISessionMembership membership,
        ISessionAdvertisementOwner advertisementOwner,
        ISessionServerReadiness serverReadiness,
        ISessionTransportTargetSource transportTargetSource,
        IMissionPeerTransport missionTransport,
        IAuthenticatedPeerIdentityResolver peerIdentityResolver,
        IPeerIdentityPublisher peerIdentityPublisher,
        params IDisposable[] providerResources)
    {
        Advertiser = advertiser;
        TunnelHost = tunnelHost;
        Membership = membership;
        AdvertisementOwner = advertisementOwner;
        ServerReadiness = serverReadiness;
        TransportTargetSource = transportTargetSource;
        MissionTransport = missionTransport;
        PeerIdentityResolver = peerIdentityResolver;
        PeerIdentityPublisher = peerIdentityPublisher;
        this.providerResources = providerResources ?? Array.Empty<IDisposable>();
    }

    public ISessionAdvertiser Advertiser { get; }
    public ISessionTunnelHost TunnelHost { get; }
    public ISessionMembership Membership { get; }
    public ISessionAdvertisementOwner AdvertisementOwner { get; }
    public ISessionServerReadiness ServerReadiness { get; }
    public ISessionTransportTargetSource TransportTargetSource { get; }
    public IMissionPeerTransport MissionTransport { get; }
    public IAuthenticatedPeerIdentityResolver PeerIdentityResolver { get; }
    public IPeerIdentityPublisher PeerIdentityPublisher { get; }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        MissionTransport.Dispose();
        Advertiser.Dispose();
        TunnelHost.Dispose();
        PeerIdentityPublisher.Dispose();
        foreach (var resource in providerResources) resource?.Dispose();
    }
}

/// <summary>Overrides identity resolution while preserving one provider runtime's other facets.</summary>
public sealed class PeerIdentityResolvingSessionProviderRuntime : ISessionProviderRuntime
{
    private readonly ISessionProviderRuntime runtime;
    private readonly IDisposable peerIdentityResolverResource;
    private bool disposed;

    public PeerIdentityResolvingSessionProviderRuntime(
        ISessionProviderRuntime runtime,
        IAuthenticatedPeerIdentityResolver peerIdentityResolver,
        IDisposable peerIdentityResolverResource)
    {
        if (runtime == null) throw new ArgumentNullException(nameof(runtime));
        if (peerIdentityResolver == null) throw new ArgumentNullException(nameof(peerIdentityResolver));
        if (peerIdentityResolverResource == null) throw new ArgumentNullException(nameof(peerIdentityResolverResource));

        this.runtime = runtime;
        PeerIdentityResolver = peerIdentityResolver;
        this.peerIdentityResolverResource = peerIdentityResolverResource;
    }

    public ISessionAdvertiser Advertiser => runtime.Advertiser;
    public ISessionTunnelHost TunnelHost => runtime.TunnelHost;
    public ISessionMembership Membership => runtime.Membership;
    public ISessionAdvertisementOwner AdvertisementOwner => runtime.AdvertisementOwner;
    public ISessionServerReadiness ServerReadiness => runtime.ServerReadiness;
    public ISessionTransportTargetSource TransportTargetSource => runtime.TransportTargetSource;
    public IMissionPeerTransport MissionTransport => runtime.MissionTransport;
    public IAuthenticatedPeerIdentityResolver PeerIdentityResolver { get; }
    public IPeerIdentityPublisher PeerIdentityPublisher => runtime.PeerIdentityPublisher;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        try
        {
            runtime.Dispose();
        }
        finally
        {
            peerIdentityResolverResource.Dispose();
        }
    }
}

/// <summary>Shared empty session facets for direct and unavailable provider capabilities.</summary>
public sealed class UnavailableSessionServices :
    ISessionMembership,
    ISessionAdvertisementOwner,
    ISessionServerReadiness,
    ISessionTransportTargetSource
{
    public static readonly UnavailableSessionServices Instance = new UnavailableSessionServices();

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

/// <summary>Readiness signal for provider services that are available immediately.</summary>
public sealed class ImmediateSessionServerReadiness : ISessionServerReadiness
{
    public static readonly ImmediateSessionServerReadiness Instance =
        new ImmediateSessionServerReadiness();

    public bool IsReady => true;

    public event Action Ready
    {
        add { }
        remove { }
    }
}
