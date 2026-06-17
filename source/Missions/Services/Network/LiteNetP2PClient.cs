using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Data;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using GameInterface.Services.Entity;
using IntroServer.Config;
using IntroServer.Data;
using IntroServer.Server;
using LiteNetLib;
using Missions.Services.Network.Messages;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Version = System.Version;

namespace Missions.Services.Network;

public class LiteNetP2PClient : INatPunchListener, INetEventListener, IUpdateable, IDisposable, IMeshNetwork
{
    private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();
    public int ConnectedPeersCount => netManager.ConnectedPeersCount;
    public int Priority => 2;

    /// <summary>
    /// The connection to the rendezvous/relay server, when one is opened via
    /// <see cref="ConnectToP2PServer"/>. Null in the pure NAT-punch flow (the live co-host path), where
    /// every connected peer is a genuine punched-through client. Kept so the eventual relay fallback can
    /// route traffic through the server when a direct punch fails, and so the server's own
    /// connect/disconnect can be told apart from a peer's (see <see cref="OnPeerDisconnected"/>).
    /// </summary>
    public NetPeer PeerServer { get; private set; }

    private readonly IPacketManager packetManager;

    private readonly NetManager netManager;
    private readonly ICommonSerializer serializer;
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly Poller poller;
    private readonly Version version = typeof(MissionTestServer).Assembly.GetName().Version;

    private string instanceId = null;

    /// <summary>
    /// This client's network identity. It is the campaign <see cref="IControllerIdProvider.ControllerId"/>
    /// so a P2P peer maps directly to its campaign player. Standalone Missions flows do not set it up
    /// front, so it is populated on first use the same way the campaign client does
    /// (see Coop.Core ValidateModuleState), which always yields a non-empty value.
    /// </summary>
    private string ControllerId
    {
        get
        {
            if (string.IsNullOrEmpty(controllerIdProvider.ControllerId))
            {
                controllerIdProvider.SetControllerFromProgramArgs();
            }
            return controllerIdProvider.ControllerId;
        }
    }

    public INetworkConfig Config { get; }

    public LiteNetP2PClient(
        INetworkConfig config,
        ICommonSerializer serializer,
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        IControllerIdProvider controllerIdProvider)
    {
        Config = config;

        this.packetManager = packetManager;
        this.serializer = serializer;
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;

        netManager = new NetManager(this)
        {
            NatPunchEnabled = true,
            DisconnectTimeout = (int)Config.DisconnectTimeout.TotalMilliseconds,
            PingInterval = (int)Config.PingInterval.TotalMilliseconds,
            ReconnectDelay = (int)Config.ReconnectDelay.TotalMilliseconds,
        };

        poller = new Poller(Update, TimeSpan.FromMilliseconds(1000 / 120));
        netManager.NatPunchModule.Init(this);
    }

    ~LiteNetP2PClient()
    {
        Dispose();
    }

    public void Dispose()
    {
        Stop();
    }

    public void Start()
    {
        if (netManager.IsRunning == false)
        {
            Logger.Debug("Starting P2P Client");
            netManager.Start();
            poller.Start();
        }
    }

    public void Stop()
    {
        Logger.Debug("Stopping P2P Client");
        DisconnectPeers();
        poller.Stop();
        netManager.Stop();
    }

    /// <summary>
    /// Drop all peers but keep the socket/poller running, so the client is reused across locations
    /// without a fragile Stop/Start (which churns the port and re-enters the Poller). The poller stays
    /// up so OnPeerDisconnected is still delivered.
    /// </summary>
    public void DisconnectPeers()
    {
        Logger.Debug("Disconnecting P2P peers (keeping socket alive)");
        // Flush queued reliable sends (notably the NetworkLeaveMission broadcast on OnEndMission)
        // before dropping the connections, so a graceful leave reliably reaches peers instead of being
        // cut off by DisconnectAll. The disconnect/timeout path stays the fallback for ungraceful exits.
        FlushReliableSends();
        netManager.DisconnectAll();

        instanceId = null;
    }

    // LiteNetLib 1.3.1 has no synchronous flush, so nudge the logic thread and wait (bounded) for each
    // connected peer's reliable queue to drain — a queued ReliableOrdered packet stays until acked, so
    // an empty queue means the leave was delivered. Runs on the game thread during mission teardown;
    // the cap keeps an unresponsive peer from hitching it for more than a frame or two.
    private void FlushReliableSends()
    {
        const int maxWaitMs = 100;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < maxWaitMs)
        {
            netManager.TriggerUpdate();

            bool pending = netManager.ConnectedPeerList.Any(peer =>
                peer.GetPacketsCountInReliableQueue(0, true) > 0 ||
                peer.GetPacketsCountInReliableQueue(0, false) > 0);

            if (pending == false) return;

            Thread.Sleep(2);
        }

        Logger.Warning("[LocationSync] Reliable send queue did not drain within {Ms}ms before disconnect", maxWaitMs);
    }

    public void Update(TimeSpan frameTime)
    {
        netManager.PollEvents();
        netManager.NatPunchModule.PollEvents();
    }

    /// <summary>
    /// Point the NAT-punch rendezvous (and relay target) at a specific server — typically the campaign
    /// server CoopClient is already connected to — instead of the compiled-in defaults.
    /// </summary>
    public void SetRendezvous(string address, int port) => Config.SetRendezvous(address, port);

    /// <summary>
    /// Opens a direct connection to the rendezvous/relay server (tracked as <see cref="PeerServer"/>).
    /// NOT used by the live co-host flow, which relies on pure NAT punch — this is the entry point for
    /// the eventual relay fallback (route packets through the server when a direct punch fails). Blocks
    /// up to one second for the handshake; returns whether it completed in time.
    /// </summary>
    public bool ConnectToP2PServer()
    {
        Start();

        Logger.Information("Connecting to P2P Server");
        string connectionAddress;
        int port;
        if (Config.NATType == NatAddressType.Internal)
        {
            connectionAddress = Config.LanAddress.ToString();
            port = Config.LanPort;
        }
        else
        {
            connectionAddress = Config.WanAddress.ToString();
            port = Config.WanPort;
        }

        Logger.Information($"Connecting to {connectionAddress}:{port}");

        ClientInfo clientInfo = new ClientInfo(
            ControllerId,
            version);

        PeerServer = netManager.Connect(connectionAddress,
                                        port,
                                        clientInfo.ToString());

        Task connectionTask = Task.Run(WaitForConnection);

        return connectionTask.Wait(TimeSpan.FromSeconds(1));
    }

    private async Task WaitForConnection()
    {
        while (PeerServer.ConnectionState != ConnectionState.Connected)
        {
            await Task.Delay(100);
        }
    }

    public void ConnectToInstance(string instanceId)
    {
        Logger.Verbose("Attempting NAT Punch");

        ConnectionToken token = new ConnectionToken(ControllerId, instanceId, Config.NATType);
        if (Config.NATType == NatAddressType.Internal)
        {
            netManager.NatPunchModule.SendNatIntroduceRequest(Config.LanAddress.ToString(), Config.LanPort, token);
        }
        else if (Config.NATType == NatAddressType.External)
        {
            netManager.NatPunchModule.SendNatIntroduceRequest(Config.WanAddress.ToString(), Config.WanPort, token);
        }

        this.instanceId = instanceId;
    }

    public void Send(NetPeer netPeer, IPacket packet)
    {
        byte[] data = serializer.Serialize(packet);
        netPeer.Send(data, packet.DeliveryMethod);
    }

    // The P2P client has no per-peer send gating, so Send already reaches the peer immediately.
    public void SendImmediate(NetPeer netPeer, IPacket packet) => Send(netPeer, packet);

    public void SendImmediate(NetPeer netPeer, IMessage message) => Send(netPeer, message);

    public void SendAllBut(NetPeer netPeer, IPacket packet)
    {
        foreach (var peer in netManager.ConnectedPeerList.Where(peer => peer != netPeer))
        {
            Send(peer, packet);
        }
    }

    public void SendAll(IPacket packet)
    {
        byte[] data = serializer.Serialize(packet);
        netManager.SendToAll(data, packet.DeliveryMethod);
    }

    public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        // No requests on client
    }

    public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
    {
        if (ConnectionToken.TryParse(token, out var connectionToken) == false)
        {
            Logger.Warning("Unable to parse connection token: {tokenString}", token);
            return;
        }

        if (type == connectionToken.NatType)
        {
            Logger.Information("Connecting P2P: {TargetEndPoint}", targetEndPoint);
            netManager.Connect(targetEndPoint, token);
        }
        else
        {
            Logger.Debug("Expected {expected} but got {actual}", type, connectionToken.NatType);
        }
    }

    public void OnPeerConnected(NetPeer peer)
    {
        // peer != PeerServer covers both modes: when connected to a rendezvous/relay server PeerServer
        // is that connection (excluded here); when co-hosting via pure NAT punch PeerServer is null, so
        // every connected peer is a genuine punched-through P2P peer.
        if (peer != PeerServer)
        {
            var peerConnectedEvent = new PeerConnected(peer);
            messageBroker.Publish(this, peerConnectedEvent);
        }

        // Proof-of-P2P diagnostic: the remote endpoint here is the OTHER CLIENT's socket, reached
        // directly. Compare its port against the rendezvous (server) port — if they differ, this is
        // a direct client-to-client link, not server-relayed.
        Logger.Information("[LocationSync] P2P link established: remote(other client)={Remote} | myP2PPort={LocalPort} | rendezvous(server)={Server}:{ServerPort}. " +
            "remote != rendezvous => DIRECT P2P (not server-relayed).",
            peer, netManager.LocalPort, Config.LanAddress, Config.LanPort);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        // Reason distinguishes a real graceful leave (RemoteConnectionClose) from a transient
        // timeout/NAT drop (Timeout/ConnectionFailed). A one-sided timeout is the suspected rejoin
        // failure: we drop the peer but it never saw us drop, so it never re-announces its join info.
        Logger.Information("[LocationSync] OnPeerDisconnected from {peer}: reason={Reason}, socketError={SocketError}",
            peer, disconnectInfo.Reason, disconnectInfo.SocketErrorCode);

        // A relay-server drop is a different event than a P2P peer leaving. PeerServer is null in the
        // pure NAT-punch flow, so this always takes the peer branch there.
        if (PeerServer != peer)
        {
            var peerDisconnectedEvent = new PeerDisconnected(peer, disconnectInfo);
            messageBroker.Publish(this, peerDisconnectedEvent);
        }
        else
        {
            ServerDisconnected serverDisconnected = new ServerDisconnected(disconnectInfo);
            messageBroker.Publish(this, serverDisconnected);
        }
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Logger.Error("Network error {socketError} sending to {endpoint}", socketError, endPoint);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {

    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {

    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        string token = request.Data.GetString();

        if (ConnectionToken.TryParse(token, out var connectionToken) == false) return;

        if (instanceId == connectionToken.InstanceId)
        {
            request.Accept();
        }
        else
        {
            Logger.Error("Incoming connection was a part of a different instance," +
                "this means there is an issue with the server");
            request.Reject();
        }
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        Send(netPeer, MessagePacket.Create(message, serializer));
    }

    public void SendAll(IMessage message)
    {
        SendAll(MessagePacket.Create(message, serializer));
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        SendAllBut(excludedPeer, MessagePacket.Create(message, serializer));
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        var packet = serializer.Deserialize<IPacket>(reader.GetRemainingBytes());

        packetManager.HandleReceive(peer, packet);
    }
}
