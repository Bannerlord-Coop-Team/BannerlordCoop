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
            return CreateClient(sdk, messageBroker, joinRequestGate);
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
    public bool IsAvailable => gameServer || sdk.LocalUserId != 0;
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

        return new SessionProviderRuntime(
            advertiser,
            tunnelHost,
            joinListener,
            advertiser,
            ImmediateSessionServerReadiness.Instance,
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
            UnavailableSessionServices.Instance,
            options.Visibility,
            dedicatedServer: true);
        var tunnelTransport = new GalaxyDatagramTransport(sdk);
        var tunnelHost = new ProviderTunnelHost(tunnelTransport);

        return new SessionProviderRuntime(
            advertiser,
            tunnelHost,
            UnavailableSessionServices.Instance,
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

    internal static GalaxySessionProvider CreateClient(
        IGalaxySdk sdk,
        IMessageBroker messageBroker,
        ISessionJoinRequestGate joinRequestGate)
    {
        if (sdk == null) throw new ArgumentNullException(nameof(sdk));

        var joinListener = new GalaxyJoinListener(messageBroker, sdk, joinRequestGate);
        var provider = new GalaxySessionProvider(
            sdk,
            joinListener,
            new GalaxyLobbyBrowser(sdk),
            new GalaxyTunnelJoinEndpointPreparer(sdk),
            gameServer: false);

        if (!provider.IsAvailable)
        {
            Logger.Warning("Galaxy identity is not authenticated yet; retrying Galaxy sign-in");
            sdk.EnsureAuthenticated(success =>
            {
                if (success)
                    Logger.Information("Galaxy identity authenticated; GOG session services are ready");
                else
                    Logger.Warning("Galaxy sign-in failed; launch Bannerlord through GOG Galaxy and retry the lobby search");
            });
        }

        return provider;
    }

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
}
