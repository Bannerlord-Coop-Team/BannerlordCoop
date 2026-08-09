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
