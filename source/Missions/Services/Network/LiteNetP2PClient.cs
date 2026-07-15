using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Data;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Missions.Services.Network;

public class LiteNetP2PClient : INatPunchListener, INetEventListener, IUpdateable, IDisposable, IBattleNetwork
{
    private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();
    public int ConnectedPeersCount => netManager.ConnectedPeersCount;
    public int Priority => 2;

    /// <summary>
    /// Optional rendezvous/relay peer. It remains distinct from direct mission peers so failed direct
    /// links can fall back through the server.
    /// </summary>
    public NetPeer PeerServer { get; private set; }

    private readonly IPacketManager packetManager;

    private readonly NetManager netManager;
    private readonly IRelayNetwork relayNetwork;
    private readonly IMissionContext missionContext;
    private readonly ICommonSerializer serializer;
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ISteamMissionBridge steamBridge;
    private readonly Poller poller;

    private readonly object peerGate = new();
    private readonly Dictionary<string, ulong> controllerSteamIds = new();
    private readonly Dictionary<NetPeer, string> pendingPeerControllers = new();
    private readonly Dictionary<NetPeer, string> mappedPeerControllers = new();
    private readonly Dictionary<NetPeer, ulong> peerSteamIds = new();
    private readonly HashSet<NetPeer> connectedPendingPeers = new();
    private bool disposed;

    private string instanceId = null;
    private int instanceGeneration;

    /// <summary>
    /// Campaign controller identity used to map a mission peer to its player. Standalone mission flows
    /// initialize it lazily from launch arguments.
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
        IRelayNetwork relayNetwork,
        IMissionContext missionContext,
        ICommonSerializer serializer,
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        IControllerIdProvider controllerIdProvider,
        ISteamMissionBridge steamBridge)
    {
        Config = config;
        this.relayNetwork = relayNetwork;
        this.missionContext = missionContext;
        this.packetManager = packetManager;
        this.serializer = serializer;
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;
        this.steamBridge = steamBridge;

        netManager = new NetManager(this)
        {
            NatPunchEnabled = true,
            DisconnectTimeout = (int)Config.DisconnectTimeout.TotalMilliseconds,
            PingInterval = (int)Config.PingInterval.TotalMilliseconds,
            ReconnectDelay = (int)Config.ReconnectDelay.TotalMilliseconds,
        };

        poller = new Poller(Update, TimeSpan.FromMilliseconds(1000 / 120));
        netManager.NatPunchModule.Init(this);

        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_MissionPeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);
        steamBridge.PeerDisconnected += Handle_SteamPeerDisconnected;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_MissionPeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);
        steamBridge.PeerDisconnected -= Handle_SteamPeerDisconnected;
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

        steamBridge.Start(netManager.LocalPort);
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
        lock (peerGate)
        {
            instanceId = null;
            instanceGeneration++;
        }
        // Flush queued reliable sends (notably the NetworkLeaveMission broadcast on OnEndMission)
        // before dropping the connections, so a graceful leave reliably reaches peers instead of being
        // cut off by DisconnectAll. The disconnect/timeout path stays the fallback for ungraceful exits.
        FlushReliableSends();
        netManager.DisconnectAll();
        steamBridge.Stop();

        lock (peerGate)
        {
            controllerSteamIds.Clear();
            pendingPeerControllers.Clear();
            mappedPeerControllers.Clear();
            peerSteamIds.Clear();
            connectedPendingPeers.Clear();
        }
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

    public void ConnectToInstance(string instanceId)
    {
        // The relay send and the connection-accept check both read this, so it is set even
        // when the punch below is skipped.
        lock (peerGate)
        {
            this.instanceId = instanceId;
            instanceGeneration++;
        }
        steamBridge.Start(netManager.LocalPort);

        // A tunneled session cannot punch: the rendezvous only observes loopback pump
        // endpoints, so mission traffic stays on the per-send server relay fallback.
        if (Config.IsTunneled) return;

        Logger.Verbose("Attempting NAT Punch");

        ConnectionToken token = new ConnectionToken(ControllerId, instanceId);

        netManager.NatPunchModule.SendNatIntroduceRequest(relayNetwork.ServerEndpoint, token);
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

        // The NAT token names the newcomer, so only existing members initiate the connection.
        if (connectionToken.ControllerId == ControllerId) return;

        lock (peerGate)
        {
            if (instanceId != connectionToken.InstanceId || HasTrackedPeer(connectionToken.ControllerId)) return;

            Logger.Information("Connecting P2P: {TargetEndPoint}", targetEndPoint);
            var peer = netManager.Connect(
                targetEndPoint,
                new ConnectionToken(ControllerId, connectionToken.InstanceId));
            if (peer != null)
            {
                pendingPeerControllers[peer] = connectionToken.ControllerId;
            }
        }
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        // Reason distinguishes a real graceful leave (RemoteConnectionClose) from a transient
        // timeout/NAT drop (Timeout/ConnectionFailed). A one-sided timeout is the suspected rejoin
        // failure: we drop the peer but it never saw us drop, so it never re-announces its join info.
        Logger.Information("[LocationSync] OnPeerDisconnected from {peer}: reason={Reason}, socketError={SocketError}",
            peer, disconnectInfo.Reason, disconnectInfo.SocketErrorCode);

        string controllerId = null;
        ulong remoteSteamId = 0;
        lock (peerGate)
        {
            RemovePeerTracking(peer, out controllerId);

            if (controllerId != null)
            {
                controllerSteamIds.TryGetValue(controllerId, out remoteSteamId);
            }
        }

        missionContext.RemovePeer(peer);
        if (remoteSteamId != 0) steamBridge.Disconnect(remoteSteamId);
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
        string token;
        try
        {
            token = request.Data.GetString(ConnectionToken.MaxSerializedLength);
        }
        catch (Exception)
        {
            request.Reject();
            return;
        }

        if (ConnectionToken.TryParse(token, out var connectionToken) == false)
        {
            request.Reject();
            return;
        }

        bool authenticated = steamBridge.TryGetRemoteSteamId(
            request.RemoteEndPoint, out var authenticatedSteamId);

        lock (peerGate)
        {
            controllerSteamIds.TryGetValue(connectionToken.ControllerId, out var expectedSteamId);
            bool expectedPeer = authenticated
                ? expectedSteamId == 0 || expectedSteamId == authenticatedSteamId
                : expectedSteamId == 0;

            if (instanceId == connectionToken.InstanceId
                && expectedPeer
                && !HasTrackedPeer(connectionToken.ControllerId))
            {
                var peer = request.Accept();
                if (peer != null)
                {
                    pendingPeerControllers[peer] = connectionToken.ControllerId;
                    if (authenticated) peerSteamIds[peer] = authenticatedSteamId;
                }
                return;
            }
        }

        Logger.Warning("Rejected a mission peer with a different instance or Steam identity");
        request.Reject();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        bool rejectPeer = false;
        lock (peerGate)
        {
            if (pendingPeerControllers.TryGetValue(peer, out var controllerId))
            {
                controllerSteamIds.TryGetValue(controllerId, out var expectedSteamId);
                peerSteamIds.TryGetValue(peer, out var actualSteamId);

                if (expectedSteamId == 0 && actualSteamId != 0)
                {
                    connectedPendingPeers.Add(peer);
                }
                else if (expectedSteamId != 0 && expectedSteamId != actualSteamId)
                {
                    RemovePeerTracking(peer, out _);
                    rejectPeer = true;
                }
                else
                {
                    PromotePeer(peer, controllerId);
                }
            }
        }

        if (rejectPeer) netManager.DisconnectPeer(peer);

        // Proof-of-P2P diagnostic: the remote endpoint here is the OTHER CLIENT's socket, reached
        // directly. Compare its port against the rendezvous (server) port — if they differ, this is
        // a direct client-to-client link, not server-relayed.
        Logger.Information("[LocationSync] P2P link established: remote(other client)={Remote} | myP2PPort={LocalPort} | rendezvous(server)={Server}:{ServerPort}. " +
            "remote != rendezvous => DIRECT P2P (not server-relayed).",
            peer, netManager.LocalPort, Config.LanAddress, Config.LanPort);
    }

    private void Handle_MissionPeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.SteamId == 0) return;

        var invalidPeers = new List<NetPeer>();
        bool alreadyTracked;
        int generation;
        lock (peerGate)
        {
            if (entered.InstanceId != instanceId) return;
            generation = instanceGeneration;
            controllerSteamIds[entered.ControllerId] = entered.SteamId;

            foreach (var pair in pendingPeerControllers
                .Where(pair => pair.Value == entered.ControllerId)
                .ToArray())
            {
                peerSteamIds.TryGetValue(pair.Key, out var actualSteamId);
                if (actualSteamId != entered.SteamId)
                {
                    RemovePeerTracking(pair.Key, out _);
                    invalidPeers.Add(pair.Key);
                }
                else if (connectedPendingPeers.Contains(pair.Key))
                {
                    PromotePeer(pair.Key, entered.ControllerId);
                }
            }

            foreach (var pair in mappedPeerControllers
                .Where(pair => pair.Value == entered.ControllerId)
                .ToArray())
            {
                peerSteamIds.TryGetValue(pair.Key, out var actualSteamId);
                if (actualSteamId == entered.SteamId) continue;

                missionContext.RemovePeer(pair.Key);
                RemovePeerTracking(pair.Key, out _);
                invalidPeers.Add(pair.Key);
            }

            alreadyTracked = HasTrackedPeer(entered.ControllerId);
        }

        foreach (var invalidPeer in invalidPeers) netManager.DisconnectPeer(invalidPeer);
        if (alreadyTracked) return;

        if (!steamBridge.TryConnect(entered.SteamId, out var endpoint)) return;

        var token = new ConnectionToken(ControllerId, entered.InstanceId);
        bool redundantConnection;
        lock (peerGate)
        {
            redundantConnection = generation != instanceGeneration
                || entered.InstanceId != instanceId
                || HasTrackedPeer(entered.ControllerId);
            if (!redundantConnection)
            {
                var peer = netManager.Connect(endpoint, token);
                if (peer != null)
                {
                    pendingPeerControllers[peer] = entered.ControllerId;
                    peerSteamIds[peer] = entered.SteamId;
                }
            }
        }

        if (redundantConnection) steamBridge.Disconnect(entered.SteamId);
    }

    private void Handle_MissionPeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        HandlePeerDeparture(payload.What.ControllerId, payload.What.InstanceId);
    }

    private void Handle_MissionPeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        HandlePeerDeparture(payload.What.ControllerId, payload.What.InstanceId);
    }

    private void HandlePeerDeparture(string controllerId, string departedInstanceId)
    {
        NetPeer trackedPeer = null;
        ulong remoteSteamId = 0;
        lock (peerGate)
        {
            if (departedInstanceId != instanceId) return;

            if (controllerSteamIds.TryGetValue(controllerId, out remoteSteamId))
            {
                controllerSteamIds.Remove(controllerId);
            }
            trackedPeer = RemoveTrackedPeer(controllerId);
        }

        if (trackedPeer != null)
        {
            missionContext.RemovePeer(trackedPeer);
            netManager.DisconnectPeer(trackedPeer);
        }

        if (remoteSteamId != 0) steamBridge.Disconnect(remoteSteamId);
    }

    private void Handle_SteamPeerDisconnected(ulong remoteSteamId)
    {
        string controllerId = null;
        NetPeer trackedPeer = null;
        lock (peerGate)
        {
            foreach (var pair in controllerSteamIds)
            {
                if (pair.Value == remoteSteamId)
                {
                    controllerId = pair.Key;
                    break;
                }
            }

            if (controllerId != null) trackedPeer = RemoveTrackedPeer(controllerId);
        }

        if (trackedPeer == null) return;

        missionContext.RemovePeer(trackedPeer);
        netManager.DisconnectPeer(trackedPeer);
    }

    private bool HasTrackedPeer(string controllerId)
    {
        return pendingPeerControllers.ContainsValue(controllerId) ||
            mappedPeerControllers.ContainsValue(controllerId);
    }

    private NetPeer RemoveTrackedPeer(string controllerId)
    {
        foreach (var pair in pendingPeerControllers.ToArray())
        {
            if (pair.Value != controllerId) continue;
            RemovePeerTracking(pair.Key, out _);
            return pair.Key;
        }

        foreach (var pair in mappedPeerControllers.ToArray())
        {
            if (pair.Value != controllerId) continue;
            RemovePeerTracking(pair.Key, out _);
            return pair.Key;
        }

        return null;
    }

    private void PromotePeer(NetPeer peer, string controllerId)
    {
        pendingPeerControllers.Remove(peer);
        connectedPendingPeers.Remove(peer);
        mappedPeerControllers[peer] = controllerId;
        missionContext.MapPeer(controllerId, peer);
    }

    private void RemovePeerTracking(NetPeer peer, out string controllerId)
    {
        if (!pendingPeerControllers.TryGetValue(peer, out controllerId))
        {
            mappedPeerControllers.TryGetValue(peer, out controllerId);
        }

        pendingPeerControllers.Remove(peer);
        mappedPeerControllers.Remove(peer);
        peerSteamIds.Remove(peer);
        connectedPendingPeers.Remove(peer);
    }

    public void SendAll(IMessage message)
    {
        foreach (var controllerId in missionContext.ControllersInMission)
        {
            Send(controllerId, message);
        }
    }

    public void SendAll(IPacket packet)
    {
        foreach (var controllerId in missionContext.ControllersInMission)
        {
            Send(controllerId, packet);
        }
    }

    public void Send(string controllerId, IMessage message)
    {
        Send(controllerId, MessagePacket.Create(message, serializer));
    }

    public void SendAllBut(string excludedId, IMessage message)
    {
        SendAllBut(excludedId, MessagePacket.Create(message, serializer));
    }

    public void SendAllBut(string excludedId, IPacket packet)
    {
        foreach (var controllerId in missionContext.ControllersInMission.Where(id => id != excludedId))
        {
            Send(controllerId, packet);
        }
    }

    public void Send(string controllerId, IPacket packet)
    {
        // Send directly to direct peer
        if (missionContext.TryGetPeer(controllerId, out var peer))
        {
            Send(peer, packet);
            return;
        }

        // Otherwise send relay packet to the server
        var payload = serializer.Serialize(packet);
        relayNetwork.SendAll(new RelayPacket(packet.DeliveryMethod, instanceId, controllerId, payload));
    }

    // A conservative ceiling for a single non-fragmentable (Unreliable/Sequenced) send. netPeer
    // .GetMaxSinglePacketSize() can read OPTIMISTICALLY high — it cleared a movement batch the send then
    // rejected at ~1 KB (TooBigPacketException), which the Poller swallowed so movement silently stopped and
    // every puppet froze. Capping the promote threshold here keeps the pre-check honest on links whose real
    // single-packet limit sits near the minimum MTU; genuinely-smaller MTUs still win via the Math.Min below.
    private const int SafeSinglePacketBytes = 1000;

    public void Send(NetPeer netPeer, IPacket packet)
    {
        byte[] data = serializer.Serialize(packet);
        var method = packet.DeliveryMethod;

        // Unreliable/Sequenced channels can't fragment: an oversized payload makes netPeer.Send throw
        // TooBigPacketException, which the Poller swallows (e.g. movement then silently stops). When a
        // packet exceeds the peer's single-packet limit, promote it to a fragmentable reliable channel so
        // LiteNetLib splits it instead of throwing. Senders chunk to keep the common case unreliable; this is
        // the backstop for whatever still overflows (small early MTU, fat all-cavalry batches, spawn bursts).
        bool fragmentable = method == DeliveryMethod.ReliableOrdered || method == DeliveryMethod.ReliableUnordered;
        if (!fragmentable && data.Length > Math.Min(netPeer.GetMaxSinglePacketSize(method), SafeSinglePacketBytes))
        {
            method = DeliveryMethod.ReliableUnordered;
            fragmentable = true;
        }

        try
        {
            netPeer.Send(data, method);
        }
        catch (TooBigPacketException) when (!fragmentable)
        {
            // The size estimate still under-shot the peer's real cap — deliver via the fragmentable reliable
            // channel rather than letting the Poller swallow the throw and drop the packet entirely.
            netPeer.Send(data, DeliveryMethod.ReliableUnordered);
        }
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        lock (peerGate)
        {
            if (!mappedPeerControllers.ContainsKey(peer)) return;
        }

        var packet = serializer.Deserialize<IPacket>(reader.GetRemainingBytes());

        packetManager.HandleReceive(peer, packet);
    }
}
