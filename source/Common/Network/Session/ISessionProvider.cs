using Common;
using System;

namespace Common.Network.Session;

/// <summary>Tracks membership in one provider-owned session listing.</summary>
public interface ISessionMembership
{
    bool IsInSession { get; }
    SessionListingId ListingId { get; }

    void JoinSession(SessionListingId listingId);
    void LeaveSession();
}

/// <summary>Lets provider adapters reject external join requests before changing lobby membership.</summary>
public interface ISessionJoinRequestGate
{
    bool CanStartJoin();
}

/// <summary>Exposes the listing currently owned by an advertiser.</summary>
public interface ISessionAdvertisementOwner
{
    SessionListingId ListingId { get; }
    event Action<SessionListingId> ListingChanged;
}

/// <summary>Signals when a provider's standalone-server identity is usable.</summary>
public interface ISessionServerReadiness
{
    bool IsReady { get; }
    event Action Ready;
}

/// <summary>Supplies the provider peer that accepts tunneled session traffic.</summary>
public interface ISessionTransportTargetSource
{
    PlatformIdentity TunnelTarget { get; }
    string PublicAddress { get; }
}

/// <summary>Session-scoped provider services consumed by Core and Missions.</summary>
public interface ISessionProviderRuntime : IDisposable
{
    ISessionAdvertiser Advertiser { get; }
    ISessionTunnelHost TunnelHost { get; }
    ISessionMembership Membership { get; }
    ISessionAdvertisementOwner AdvertisementOwner { get; }
    ISessionServerReadiness ServerReadiness { get; }
    ISessionTransportTargetSource TransportTargetSource { get; }
    IMissionPeerTransport MissionTransport { get; }
    IAuthenticatedPeerIdentityResolver PeerIdentityResolver { get; }
    IPeerIdentityPublisher PeerIdentityPublisher { get; }
}

/// <summary>Provider-neutral inputs used to compose one client or server runtime.</summary>
public sealed class SessionProviderRuntimeOptions
{
    public ServerVisibility Visibility { get; set; }
    public string PeerIdentityBridgeName { get; set; }
}

/// <summary>Process-lifetime storefront adapter selected by the composition root.</summary>
public interface ISessionProvider : IDisposable
{
    string Provider { get; }
    string DisplayName { get; }
    bool IsAvailable { get; }
    bool SupportsDedicatedServer { get; }
    ISessionBrowser Browser { get; }
    ITunnelJoinEndpointPreparer JoinEndpointPreparer { get; }
    IUpdateable CallbackPump { get; }

    ISessionProviderRuntime CreateClientRuntime(SessionProviderRuntimeOptions options);
    ISessionProviderRuntime CreateServerRuntime(SessionProviderRuntimeOptions options);
}
