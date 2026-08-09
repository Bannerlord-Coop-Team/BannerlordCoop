using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Session;
using Serilog;
using System;

namespace Coop.GOG;

/// <summary>Process-lifetime GOG Galaxy session adapter.</summary>
public sealed class GalaxySessionProvider : ISessionProvider
{
    public const string ProviderId = "gog";

    private static readonly ILogger Logger = LogManager.GetLogger<GalaxySessionProvider>();

    private readonly IGalaxySdk sdk;
    private readonly GalaxyJoinListener joinListener;
    private readonly GalaxyLobbyBrowser browser;
    private readonly GalaxyTunnelJoinEndpointPreparer joinEndpointPreparer;
    private readonly bool gameServer;
    private bool disposed;

    private GalaxySessionProvider(
        IGalaxySdk sdk,
        GalaxyJoinListener joinListener,
        GalaxyLobbyBrowser browser,
        GalaxyTunnelJoinEndpointPreparer joinEndpointPreparer,
        bool gameServer)
    {
        this.sdk = sdk;
        this.joinListener = joinListener;
        this.browser = browser;
        this.joinEndpointPreparer = joinEndpointPreparer;
        this.gameServer = gameServer;
    }

    public static GalaxySessionProvider TryCreateClient(
        IMessageBroker messageBroker,
        ISessionJoinRequestGate joinRequestGate)
    {
        GalaxySdk sdk = null;
        try
        {
            sdk = new GalaxySdk(gameServer: false);
            if (sdk.LocalUserId == 0)
            {
                sdk.Dispose();
                Logger.Warning("Galaxy session integration unavailable: local Galaxy identity is invalid");
                return null;
            }

            var joinListener = new GalaxyJoinListener(messageBroker, sdk, joinRequestGate);
            return new GalaxySessionProvider(
                sdk,
                joinListener,
                new GalaxyLobbyBrowser(sdk),
                new GalaxyTunnelJoinEndpointPreparer(sdk),
                gameServer: false);
        }
        catch (Exception ex)
        {
            sdk?.Dispose();
            Logger.Warning(ex, "Galaxy client session integration unavailable");
            return null;
        }
    }

    public static GalaxySessionProvider TryCreateServer()
    {
        if (!GalaxyGameServerBoot.TryStart()) return null;

        try
        {
            return new GalaxySessionProvider(
                new GalaxySdk(gameServer: true),
                null,
                null,
                null,
                gameServer: true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Galaxy game-server session services unavailable");
            GalaxyGameServerBoot.Shutdown();
            return null;
        }
    }

    public string Provider => ProviderId;
    public string DisplayName => "GOG";
    public bool SupportsDedicatedServer => GalaxyGameServerBoot.HasConfiguredCredentials;
    public ISessionBrowser Browser => browser;
    public ITunnelJoinEndpointPreparer JoinEndpointPreparer => joinEndpointPreparer;
    public IUpdateable CallbackPump => gameServer ? new GalaxyGameServerCallbackPump() : null;

    public ISessionProviderRuntime CreateClientRuntime(SessionProviderRuntimeOptions options)
    {
        if (gameServer) throw new InvalidOperationException("The Galaxy game-server adapter cannot create a client runtime");
        if (options == null) throw new ArgumentNullException(nameof(options));

        var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            joinListener,
            options.Visibility,
            dedicatedServer: false);
        var identityPublisher = CreateIdentityPublisher(options.PeerIdentityBridgeName);
        var tunnelTransport = new GalaxyDatagramTransport(sdk);
        var tunnelHost = new ProviderTunnelHost(
            tunnelTransport,
            identityPublisher: identityPublisher);
        var missionTransport = new ProviderMissionPeerTransport(
            new GalaxyDatagramTransport(sdk),
            () => new GalaxyDatagramTransport(sdk));

        return new GalaxySessionProviderRuntime(
            advertiser,
            tunnelHost,
            joinListener,
            advertiser,
            ImmediateServerReadiness.Instance,
            new GalaxyTransportTargetSource(sdk),
            missionTransport,
            tunnelHost,
            identityPublisher,
            tunnelTransport);
    }

    public ISessionProviderRuntime CreateServerRuntime(SessionProviderRuntimeOptions options)
    {
        if (!gameServer) throw new InvalidOperationException("The Galaxy client adapter cannot create a server runtime");
        if (options == null) throw new ArgumentNullException(nameof(options));

        var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            UnavailableSessionMembership.Instance,
            options.Visibility,
            dedicatedServer: true);
        var tunnelTransport = new GalaxyDatagramTransport(sdk);
        var tunnelHost = new ProviderTunnelHost(tunnelTransport);

        return new GalaxySessionProviderRuntime(
            advertiser,
            tunnelHost,
            UnavailableSessionMembership.Instance,
            advertiser,
            new GalaxyGameServerReadiness(),
            new GalaxyTransportTargetSource(sdk),
            new NoopMissionPeerTransport(),
            tunnelHost,
            NoopPeerIdentityPublisher.Instance,
            tunnelTransport);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        joinEndpointPreparer?.TearDown();
        joinListener?.Dispose();
        sdk.Dispose();
        if (gameServer) GalaxyGameServerBoot.Shutdown();
    }

    private static IPeerIdentityPublisher CreateIdentityPublisher(string bridgeName) =>
        PeerIdentityBridgeName.IsValid(bridgeName)
            ? new NamedPipePeerIdentityPublisher(bridgeName)
            : NoopPeerIdentityPublisher.Instance;

    private sealed class GalaxyTransportTargetSource : ISessionTransportTargetSource
    {
        private readonly IGalaxySdk sdk;

        public GalaxyTransportTargetSource(IGalaxySdk sdk)
        {
            this.sdk = sdk;
        }

        public PlatformIdentity TunnelTarget => GalaxyDatagramTransport.GalaxyIdentity(sdk.LocalUserId);
        public string PublicAddress => string.Empty;
    }

    private sealed class GalaxyGameServerReadiness : ISessionServerReadiness
    {
        public bool IsReady => GalaxyGameServerBoot.IsReady;

        public event Action Ready
        {
            add => GalaxyGameServerBoot.Ready += value;
            remove => GalaxyGameServerBoot.Ready -= value;
        }
    }

    private sealed class ImmediateServerReadiness : ISessionServerReadiness
    {
        public static readonly ImmediateServerReadiness Instance = new ImmediateServerReadiness();
        public bool IsReady => true;

        public event Action Ready
        {
            add { }
            remove { }
        }
    }

    private sealed class UnavailableSessionMembership : ISessionMembership
    {
        public static readonly UnavailableSessionMembership Instance = new UnavailableSessionMembership();
        public bool IsInSession => false;
        public SessionListingId ListingId => default;
        public void JoinSession(SessionListingId listingId) { }
        public void LeaveSession() { }
    }

    private sealed class GalaxySessionProviderRuntime : ISessionProviderRuntime
    {
        private readonly IDisposable tunnelTransport;
        private bool disposed;

        public GalaxySessionProviderRuntime(
            ISessionAdvertiser advertiser,
            ISessionTunnelHost tunnelHost,
            ISessionMembership membership,
            ISessionAdvertisementOwner advertisementOwner,
            ISessionServerReadiness serverReadiness,
            ISessionTransportTargetSource transportTargetSource,
            IMissionPeerTransport missionTransport,
            IAuthenticatedPeerIdentityResolver peerIdentityResolver,
            IPeerIdentityPublisher peerIdentityPublisher,
            IDisposable tunnelTransport)
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
            this.tunnelTransport = tunnelTransport;
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
            tunnelTransport.Dispose();
        }
    }
}
