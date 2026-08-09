using Common;
using Common.Messaging;
using Common.Network.Session;
using Steamworks;
using System;

namespace Coop.Steam;

/// <summary>Process-lifetime Steam session adapter.</summary>
public sealed class SteamSessionProvider : ISessionProvider
{
    public const string ProviderId = "steam";

    private readonly SteamJoinListener joinListener;
    private readonly SteamLobbyBrowser browser;
    private readonly SteamTunnelJoinEndpointPreparer joinEndpointPreparer;
    private readonly bool gameServer;
    private bool disposed;

    internal SteamSessionProvider(
        SteamJoinListener joinListener,
        SteamLobbyBrowser browser,
        SteamTunnelJoinEndpointPreparer joinEndpointPreparer,
        bool gameServer)
    {
        this.joinListener = joinListener;
        this.browser = browser;
        this.joinEndpointPreparer = joinEndpointPreparer;
        this.gameServer = gameServer;
    }

    public static SteamSessionProvider TryCreateClient(
        IMessageBroker messageBroker,
        string commandLine,
        ISessionJoinRequestGate joinRequestGate)
    {
        return SteamBoot.TryStart(messageBroker, commandLine, joinRequestGate)
            ? SteamBoot.SessionProvider
            : null;
    }

    public static SteamSessionProvider TryCreateServer()
    {
        return SteamGameServerBoot.TryStart()
            ? new SteamSessionProvider(null, null, null, gameServer: true)
            : null;
    }

    public string Provider => ProviderId;
    public string DisplayName => "Steam";
    public bool SupportsDedicatedServer => true;
    public ISessionBrowser Browser => browser;
    public ITunnelJoinEndpointPreparer JoinEndpointPreparer => joinEndpointPreparer;
    public IUpdateable CallbackPump => gameServer ? new GameServerCallbackPump() : null;

    public ISessionProviderRuntime CreateClientRuntime(SessionProviderRuntimeOptions options)
    {
        if (gameServer) throw new InvalidOperationException("The Steam game-server adapter cannot create a client runtime");
        if (options == null) throw new ArgumentNullException(nameof(options));

        var lobbyApi = new SteamLobbyApi();
        var advertiser = new SteamLobbyAdvertiser(lobbyApi, joinListener);
        var identityPublisher = CreateIdentityPublisher(options.PeerIdentityBridgeName);
        var tunnelTransport = new SteamDatagramTransportAdapter(
            new SteamNetworkingTunnelTransport(),
            GetUserSteamId);
        var tunnelHost = new ProviderTunnelHost(
            tunnelTransport,
            identityPublisher: identityPublisher);
        var missionTransport = new ProviderMissionPeerTransport(
            new SteamDatagramTransportAdapter(new SteamNetworkingTunnelTransport(), GetUserSteamId),
            () => new SteamDatagramTransportAdapter(new SteamNetworkingTunnelTransport(), GetUserSteamId));

        return new SessionProviderRuntime(
            advertiser,
            tunnelHost,
            joinListener,
            advertiser,
            Common.Network.Session.ImmediateSessionServerReadiness.Instance,
            new SteamTransportTargetSource(gameServer: false),
            missionTransport,
            tunnelHost,
            identityPublisher,
            tunnelTransport,
            lobbyApi);
    }

    public ISessionProviderRuntime CreateServerRuntime(SessionProviderRuntimeOptions options)
    {
        if (!gameServer) throw new InvalidOperationException("The Steam client adapter cannot create a server runtime");
        if (options == null) throw new ArgumentNullException(nameof(options));

        var lobbyApi = new SteamLobbyApi();
        var leaseRenewer = new SteamLobbyLeaseRenewer();
        var advertiser = new SteamPublicLobbyAdvertiser(lobbyApi, options.Visibility, leaseRenewer);
        var tunnelTransport = new SteamDatagramTransportAdapter(
            new SteamGameServerNetworkingTunnelTransport(),
            () => SteamGameServerBoot.GameServerSteamId);
        var tunnelHost = new ProviderTunnelHost(tunnelTransport);

        return new SessionProviderRuntime(
            advertiser,
            tunnelHost,
            UnavailableSessionServices.Instance,
            advertiser,
            new SteamGameServerReadiness(),
            new SteamTransportTargetSource(gameServer: true),
            new NoopMissionPeerTransport(),
            tunnelHost,
            NoopPeerIdentityPublisher.Instance,
            tunnelTransport,
            lobbyApi);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        joinEndpointPreparer?.TearDown();
        joinListener?.Dispose();
        if (gameServer) SteamGameServerBoot.Shutdown();
    }

    private static ulong GetUserSteamId() => SteamUser.GetSteamID().m_SteamID;

    private static IPeerIdentityPublisher CreateIdentityPublisher(string bridgeName) =>
        PeerIdentityBridgeName.IsValid(bridgeName)
            ? new NamedPipePeerIdentityPublisher(bridgeName)
            : NoopPeerIdentityPublisher.Instance;

    private sealed class SteamTransportTargetSource : ISessionTransportTargetSource
    {
        private readonly bool gameServer;

        public SteamTransportTargetSource(bool gameServer)
        {
            this.gameServer = gameServer;
        }

        public PlatformIdentity TunnelTarget => SteamDatagramTransportAdapter.SteamIdentity(
            gameServer ? SteamGameServerBoot.GameServerSteamId : GetUserSteamId());

        public string PublicAddress => gameServer ? SteamGameServerBoot.PublicIp : string.Empty;
    }

    private sealed class SteamGameServerReadiness : ISessionServerReadiness
    {
        public bool IsReady => SteamGameServerBoot.IsLoggedOn;

        public event Action Ready
        {
            add => SteamGameServerBoot.LoggedOn += value;
            remove => SteamGameServerBoot.LoggedOn -= value;
        }
    }

}
