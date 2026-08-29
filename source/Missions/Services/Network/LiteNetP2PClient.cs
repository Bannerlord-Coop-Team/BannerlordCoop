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
#if DEBUG
using Missions.Agents.Packets;
using Missions.Diagnostics;
#endif
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
    private readonly IMovementPacketCompressor movementPacketCompressor;
    private readonly Poller poller;

    private readonly object peerGate = new();
    private readonly Dictionary<string, ulong> controllerSteamIds = new();
    private readonly Dictionary<string, Guid> controllerPeerCredentials = new();
    private readonly Dictionary<NetPeer, string> pendingPeerControllers = new();
    private readonly Dictionary<NetPeer, string> mappedPeerControllers = new();
    private readonly Dictionary<NetPeer, ulong> peerSteamIds = new();
    private readonly Dictionary<NetPeer, Guid> peerCredentials = new();
    private readonly Dictionary<(string ControllerId, Guid PeerCredential), IPEndPoint> deferredNatIntroductions = new();
    private readonly HashSet<NetPeer> connectedPendingPeers = new();
    private readonly HashSet<NetPeer> rotatingPendingPeers = new();
    private readonly object relayPayloadBudgetGate = new();
    private readonly Dictionary<(string InstanceId, string ControllerId), int> relayPayloadBudgets = new();
    private bool disposed;

    private string instanceId = null;
    private Guid localPeerCredential;
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
        ISteamMissionBridge steamBridge,
        IMovementPacketCompressor movementPacketCompressor)
    {
        Config = config;
        this.relayNetwork = relayNetwork;
        this.missionContext = missionContext;
        this.packetManager = packetManager;
        this.serializer = serializer;
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;
        this.steamBridge = steamBridge;
        this.movementPacketCompressor = movementPacketCompressor;

        netManager = new NetManager(this)
        {
            NatPunchEnabled = true,
            DisconnectTimeout = (int)Config.DisconnectTimeout.TotalMilliseconds,
            PingInterval = (int)Config.PingInterval.TotalMilliseconds,
            ReconnectDelay = (int)Config.ReconnectDelay.TotalMilliseconds,
        };

        poller = new Poller(Update, TimeSpan.FromMilliseconds(1000 / 120));
        netManager.NatPunchModule.Init(this);

        messageBroker.Subscribe<NetworkMissionCredentialIssued>(Handle_MissionCredentialIssued);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_MissionPeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);
        steamBridge.PeerDisconnected += Handle_SteamPeerDisconnected;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        messageBroker.Unsubscribe<NetworkMissionCredentialIssued>(Handle_MissionCredentialIssued);
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
            localPeerCredential = Guid.Empty;
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
            controllerPeerCredentials.Clear();
            pendingPeerControllers.Clear();
            mappedPeerControllers.Clear();
            peerSteamIds.Clear();
            peerCredentials.Clear();
            deferredNatIntroductions.Clear();
            connectedPendingPeers.Clear();
            rotatingPendingPeers.Clear();
        }

        lock (relayPayloadBudgetGate)
        {
            relayPayloadBudgets.Clear();
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
        // The server issues the credential after it accepts NetworkMissionEntered. Until that reliable
        // response arrives this instance may receive socket callbacks, but none can be mapped.
        lock (peerGate)
        {
            this.instanceId = instanceId;
            localPeerCredential = Guid.Empty;
            instanceGeneration++;
        }
        steamBridge.Start(netManager.LocalPort);
    }

    private void Handle_MissionCredentialIssued(MessagePayload<NetworkMissionCredentialIssued> payload)
    {
        var issued = payload.What;
        if (issued.PeerCredential == Guid.Empty) return;

        NetPeer[] trackedPeers = Array.Empty<NetPeer>();
        NetPeer[] mappedPeers = Array.Empty<NetPeer>();
        lock (peerGate)
        {
            if (issued.InstanceId != instanceId || issued.PeerCredential == localPeerCredential) return;

            if (localPeerCredential != Guid.Empty)
            {
                trackedPeers = pendingPeerControllers.Keys
                    .Concat(mappedPeerControllers.Keys)
                    .Distinct()
                    .ToArray();
                mappedPeers = mappedPeerControllers.Keys.ToArray();
                foreach (var peer in trackedPeers) RemovePeerTracking(peer, out _);
            }

            localPeerCredential = issued.PeerCredential;
        }

        foreach (var peer in mappedPeers) missionContext.RemovePeer(peer);
        foreach (var peer in trackedPeers) netManager.DisconnectPeer(peer);

        // A tunneled campaign session cannot punch because the rendezvous observes only loopback pump
        // endpoints. Steam peer announcements can still establish the direct mission link.
        if (Config.IsTunneled) return;

        Logger.Verbose("Attempting NAT Punch");
        var token = new ConnectionToken(ControllerId, issued.InstanceId, issued.PeerCredential);
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
            if (instanceId != connectionToken.InstanceId ||
                localPeerCredential == Guid.Empty)
            {
                return;
            }

            if (controllerPeerCredentials.TryGetValue(
                    connectionToken.ControllerId,
                    out var announcedCredential) &&
                announcedCredential != connectionToken.PeerCredential)
            {
                deferredNatIntroductions[(
                    connectionToken.ControllerId,
                    connectionToken.PeerCredential)] = targetEndPoint;
                return;
            }

            if (HasTrackedPeer(connectionToken.ControllerId, connectionToken.PeerCredential)) return;

            Logger.Information("Connecting P2P: {TargetEndPoint}", targetEndPoint);
            var peer = netManager.Connect(
                targetEndPoint,
                new ConnectionToken(
                    ControllerId,
                    connectionToken.InstanceId,
                    localPeerCredential));
            if (peer != null)
            {
                pendingPeerControllers[peer] = connectionToken.ControllerId;
                peerCredentials[peer] = connectionToken.PeerCredential;
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
        bool hasRemainingSteamRoute = false;
        lock (peerGate)
        {
            peerSteamIds.TryGetValue(peer, out remoteSteamId);
            RemovePeerTracking(peer, out controllerId);

            if (controllerId != null)
            {
                if (remoteSteamId == 0)
                {
                    controllerSteamIds.TryGetValue(controllerId, out remoteSteamId);
                }
                if (remoteSteamId != 0)
                {
                    hasRemainingSteamRoute = HasTrackedPeer(controllerId, remoteSteamId);
                }
            }
        }

        if (controllerId != null)
        {
            missionContext.RemovePeer(peer);
            if (remoteSteamId != 0 && !hasRemainingSteamRoute)
            {
                steamBridge.Disconnect(remoteSteamId);
            }
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

        if (connectionToken.PeerCredential == Guid.Empty)
        {
            request.Reject();
            return;
        }

        bool authenticated = steamBridge.TryGetRemoteSteamId(
            request.RemoteEndPoint, out var authenticatedSteamId);

        lock (peerGate)
        {
            controllerSteamIds.TryGetValue(connectionToken.ControllerId, out var expectedSteamId);
            bool steamIdentityMatches = authenticated
                ? expectedSteamId == 0 || expectedSteamId == authenticatedSteamId
                : expectedSteamId == 0;
            bool hasAnnouncedCredential = controllerPeerCredentials.TryGetValue(
                connectionToken.ControllerId,
                out var expectedCredential);
            bool credentialMatches = !hasAnnouncedCredential ||
                expectedCredential == connectionToken.PeerCredential;
            bool credentialRotationPending =
                authenticated &&
                steamIdentityMatches &&
                hasAnnouncedCredential &&
                expectedCredential != connectionToken.PeerCredential;

            if (instanceId == connectionToken.InstanceId
                && steamIdentityMatches
                && (credentialMatches || credentialRotationPending)
                && !HasTrackedPeer(
                    connectionToken.ControllerId,
                    connectionToken.PeerCredential))
            {
                var peer = request.Accept();
                if (peer != null)
                {
                    pendingPeerControllers[peer] = connectionToken.ControllerId;
                    if (authenticated) peerSteamIds[peer] = authenticatedSteamId;
                    peerCredentials[peer] = connectionToken.PeerCredential;
                    if (credentialRotationPending) rotatingPendingPeers.Add(peer);
                }
                return;
            }
        }

        Logger.Warning("Rejected a mission peer with a different instance, credential, or Steam identity");
        request.Reject();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        bool rejectPeer = false;
        bool mappedPeer = false;
        lock (peerGate)
        {
            if (pendingPeerControllers.TryGetValue(peer, out var controllerId))
            {
                controllerSteamIds.TryGetValue(controllerId, out var expectedSteamId);
                peerSteamIds.TryGetValue(peer, out var actualSteamId);
                peerCredentials.TryGetValue(peer, out var actualCredential);

                if (!controllerPeerCredentials.TryGetValue(controllerId, out var expectedCredential))
                {
                    connectedPendingPeers.Add(peer);
                }
                else if (actualCredential != expectedCredential)
                {
                    if (rotatingPendingPeers.Contains(peer))
                        connectedPendingPeers.Add(peer);
                    else
                    {
                        RemovePeerTracking(peer, out _);
                        rejectPeer = true;
                    }
                }
                else if (expectedSteamId != 0 && expectedSteamId != actualSteamId)
                {
                    RemovePeerTracking(peer, out _);
                    rejectPeer = true;
                }
                else
                {
                    PromotePeer(peer, controllerId);
                    mappedPeer = true;
                }
            }
        }

        if (rejectPeer) netManager.DisconnectPeer(peer);

        if (mappedPeer)
        {
            Logger.Information(
                "[LocationSync] Credential-matched P2P link established: remote={Remote} localPort={LocalPort}",
                peer,
                netManager.LocalPort);
        }
        else if (!rejectPeer)
        {
            Logger.Debug(
                "[LocationSync] P2P socket connected and is waiting for its server credential announcement: remote={Remote}",
                peer);
        }
    }

    private void Handle_MissionPeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.PeerCredential == Guid.Empty)
        {
            Logger.Warning(
                "Ignoring mission peer {ControllerId} in {InstanceId} without a server credential",
                entered.ControllerId,
                entered.InstanceId);
            return;
        }

        var invalidPeers = new List<NetPeer>();
        IPEndPoint deferredNatEndPoint;
        bool alreadyTracked;
        int generation;
        lock (peerGate)
        {
            if (entered.InstanceId != instanceId) return;
            generation = instanceGeneration;
            controllerSteamIds[entered.ControllerId] = entered.SteamId;
            controllerPeerCredentials[entered.ControllerId] = entered.PeerCredential;

            foreach (var pair in pendingPeerControllers
                .Where(pair => pair.Value == entered.ControllerId)
                .ToArray())
            {
                peerSteamIds.TryGetValue(pair.Key, out var actualSteamId);
                peerCredentials.TryGetValue(pair.Key, out var actualCredential);
                if (actualCredential != entered.PeerCredential ||
                    (entered.SteamId != 0 && actualSteamId != entered.SteamId))
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
                peerCredentials.TryGetValue(pair.Key, out var actualCredential);
                if (actualCredential == entered.PeerCredential &&
                    (entered.SteamId == 0 || actualSteamId == entered.SteamId))
                {
                    continue;
                }

                missionContext.RemovePeer(pair.Key);
                RemovePeerTracking(pair.Key, out _);
                invalidPeers.Add(pair.Key);
            }

            deferredNatIntroductions.TryGetValue(
                (entered.ControllerId, entered.PeerCredential),
                out deferredNatEndPoint);
            alreadyTracked = HasTrackedPeer(entered.ControllerId);
        }

        foreach (var invalidPeer in invalidPeers) netManager.DisconnectPeer(invalidPeer);
        if (alreadyTracked) return;

        Guid ownCredential;
        lock (peerGate)
        {
            ownCredential = localPeerCredential;
        }
        if (ownCredential == Guid.Empty) return;

        if (deferredNatEndPoint != null && entered.SteamId == 0)
        {
            lock (peerGate)
            {
                if (generation != instanceGeneration ||
                    entered.InstanceId != instanceId ||
                    HasTrackedPeer(entered.ControllerId))
                {
                    return;
                }

                Logger.Information("Connecting P2P: {TargetEndPoint}", deferredNatEndPoint);
                var peer = netManager.Connect(
                    deferredNatEndPoint,
                    new ConnectionToken(
                        ControllerId,
                        entered.InstanceId,
                        localPeerCredential));
                if (peer != null)
                {
                    deferredNatIntroductions.Remove(
                        (entered.ControllerId, entered.PeerCredential));
                    pendingPeerControllers[peer] = entered.ControllerId;
                    peerCredentials[peer] = entered.PeerCredential;
                }
            }
            return;
        }

        // A zero Steam id is a valid server announcement for direct campaign peers. The credential
        // still authenticates any inbound/NAT socket; Steam is needed only to initiate a Steam link.
        if (entered.SteamId == 0) return;
        if (!steamBridge.TryConnect(entered.SteamId, out var endpoint)) return;

        var token = new ConnectionToken(ControllerId, entered.InstanceId, ownCredential);
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
                    peerCredentials[peer] = entered.PeerCredential;
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
        List<(NetPeer Peer, bool Mapped)> trackedPeers;
        ulong remoteSteamId = 0;
        lock (peerGate)
        {
            if (departedInstanceId != instanceId) return;

            if (controllerSteamIds.TryGetValue(controllerId, out remoteSteamId))
            {
                controllerSteamIds.Remove(controllerId);
            }
            controllerPeerCredentials.Remove(controllerId);
            RemoveDeferredNatIntroductions(controllerId);
            trackedPeers = RemoveTrackedPeers(controllerId);
        }

        foreach (var (peer, mapped) in trackedPeers)
        {
            if (mapped) missionContext.RemovePeer(peer);
            netManager.DisconnectPeer(peer);
        }

        if (remoteSteamId != 0) steamBridge.Disconnect(remoteSteamId);
    }

    private void Handle_SteamPeerDisconnected(ulong remoteSteamId)
    {
        string controllerId = null;
        List<(NetPeer Peer, bool Mapped)> trackedPeers = null;
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

            if (controllerId != null) trackedPeers = RemoveTrackedPeers(controllerId);
        }

        if (trackedPeers == null) return;

        foreach (var (peer, mapped) in trackedPeers)
        {
            if (mapped) missionContext.RemovePeer(peer);
            netManager.DisconnectPeer(peer);
        }
    }

    private bool HasTrackedPeer(string controllerId)
    {
        return pendingPeerControllers.ContainsValue(controllerId) ||
            mappedPeerControllers.ContainsValue(controllerId);
    }

    private bool HasTrackedPeer(string controllerId, Guid peerCredential)
    {
        foreach (var pair in pendingPeerControllers)
        {
            if (pair.Value == controllerId &&
                peerCredentials.TryGetValue(pair.Key, out var trackedCredential) &&
                trackedCredential == peerCredential)
            {
                return true;
            }
        }

        foreach (var pair in mappedPeerControllers)
        {
            if (pair.Value == controllerId &&
                peerCredentials.TryGetValue(pair.Key, out var trackedCredential) &&
                trackedCredential == peerCredential)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasTrackedPeer(string controllerId, ulong remoteSteamId)
    {
        foreach (var pair in pendingPeerControllers)
        {
            if (pair.Value == controllerId &&
                peerSteamIds.TryGetValue(pair.Key, out var trackedSteamId) &&
                trackedSteamId == remoteSteamId)
            {
                return true;
            }
        }

        foreach (var pair in mappedPeerControllers)
        {
            if (pair.Value == controllerId &&
                peerSteamIds.TryGetValue(pair.Key, out var trackedSteamId) &&
                trackedSteamId == remoteSteamId)
            {
                return true;
            }
        }

        return false;
    }

    private List<(NetPeer Peer, bool Mapped)> RemoveTrackedPeers(string controllerId)
    {
        var peers = new List<(NetPeer Peer, bool Mapped)>();
        foreach (var pair in pendingPeerControllers.ToArray())
        {
            if (pair.Value != controllerId) continue;
            peers.Add((pair.Key, false));
            RemovePeerTracking(pair.Key, out _);
        }

        foreach (var pair in mappedPeerControllers.ToArray())
        {
            if (pair.Value != controllerId) continue;
            peers.Add((pair.Key, true));
            RemovePeerTracking(pair.Key, out _);
        }

        return peers;
    }

    private void RemoveDeferredNatIntroductions(string controllerId)
    {
        foreach (var key in deferredNatIntroductions.Keys
            .Where(key => key.ControllerId == controllerId)
            .ToArray())
        {
            deferredNatIntroductions.Remove(key);
        }
    }

    private void PromotePeer(NetPeer peer, string controllerId)
    {
        pendingPeerControllers.Remove(peer);
        connectedPendingPeers.Remove(peer);
        rotatingPendingPeers.Remove(peer);
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
        peerCredentials.Remove(peer);
        connectedPendingPeers.Remove(peer);
        rotatingPendingPeers.Remove(peer);
    }

#if DEBUG
    internal bool TryGetPeerRouteState(
        string controllerId,
        out bool credentialAnnounced,
        out bool routeExists,
        out bool credentialMatched,
        out bool steamIdentityMatched,
        out bool mapped)
    {
        credentialAnnounced = false;
        routeExists = false;
        credentialMatched = false;
        steamIdentityMatched = false;
        mapped = false;

        if (string.IsNullOrEmpty(controllerId)) return false;

        lock (peerGate)
        {
            credentialAnnounced = controllerPeerCredentials.TryGetValue(
                controllerId,
                out Guid expectedCredential);
            controllerSteamIds.TryGetValue(controllerId, out ulong expectedSteamId);

            NetPeer peer = mappedPeerControllers
                .FirstOrDefault(pair => pair.Value == controllerId)
                .Key;
            mapped = peer != null;
            if (peer == null)
            {
                peer = pendingPeerControllers
                    .FirstOrDefault(pair => pair.Value == controllerId)
                    .Key;
            }

            routeExists = peer != null;
            if (peer != null)
            {
                credentialMatched = credentialAnnounced &&
                    peerCredentials.TryGetValue(peer, out Guid presentedCredential) &&
                    presentedCredential == expectedCredential;
                peerSteamIds.TryGetValue(peer, out ulong actualSteamId);
                steamIdentityMatched = expectedSteamId == 0 || actualSteamId == expectedSteamId;
            }

            return credentialAnnounced || routeExists;
        }
    }
#endif

    public void SendAll(IMessage message)
    {
        foreach (var controllerId in missionContext.ControllersInMission)
        {
            Send(controllerId, message);
        }
    }

    public void SendAll(IPacket packet)
    {
        SendAll(packet, movementPacketCompressor.Serialize(packet));
    }

    public void SendAll(IPacket packet, byte[] serializedPacket)
    {
        foreach (var controllerId in missionContext.ControllersInMission)
        {
            Send(controllerId, packet, serializedPacket);
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
        byte[] data = movementPacketCompressor.Serialize(packet);
        foreach (var controllerId in missionContext.ControllersInMission.Where(id => id != excludedId))
        {
            Send(controllerId, packet, data);
        }
    }

    public void Send(string controllerId, IPacket packet)
    {
        Send(controllerId, packet, movementPacketCompressor.Serialize(packet));
    }

    public void Send(string controllerId, IPacket packet, byte[] data)
    {
        // Send directly to direct peer
        if (missionContext.TryGetPeer(controllerId, out var peer))
        {
            Send(peer, packet, data);
            return;
        }

        // Otherwise send relay packet to the server
        string relayInstanceId;
        lock (peerGate)
        {
            relayInstanceId = instanceId;
        }

        if (IsMovementPacket(packet))
        {
            int maxRelayPayloadBytes =
                GetMaxRelayPayloadBytes(relayInstanceId, controllerId);
            if (data.Length > maxRelayPayloadBytes)
            {
                if (maxRelayPayloadBytes > 0)
                {
                    Logger.Warning(
                        "[BattleTraffic] Discarding oversized {PacketType} relay payload for {ControllerId}: " +
                        "{PayloadBytes} bytes, budget={BudgetBytes}",
                        packet.PacketType,
                        controllerId,
                        data.Length,
                        maxRelayPayloadBytes);
                }
                return;
            }
        }

        relayNetwork.SendAll(new RelayPacket(
            packet.DeliveryMethod,
            relayInstanceId,
            controllerId,
            data));
    }

    // Peer-reported MTUs can be optimistic, so cap nonfragmentable sends at a conservative ceiling.
    internal const int SafeSinglePacketBytes = 1000;

    public int GetMaxUnreliablePayloadBytes(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return 0;

        if (missionContext.TryGetPeer(controllerId, out NetPeer peer))
        {
            return Math.Min(
                SafeSinglePacketBytes,
                Math.Max(0, peer.GetMaxSinglePacketSize(DeliveryMethod.Unreliable)));
        }

        string currentInstanceId;
        lock (peerGate)
        {
            currentInstanceId = instanceId;
        }

        return Math.Min(
            SafeSinglePacketBytes,
            Math.Max(0, GetMaxRelayPayloadBytes(currentInstanceId, controllerId)));
    }

    public int GetMaxUnreliablePayloadBytes()
    {
        int maxPayloadBytes = SafeSinglePacketBytes;
        bool hasRoute = false;
        bool hasViableRoute = false;

        foreach (string controllerId in missionContext.ControllersInMission)
        {
            hasRoute = true;
            int routePayloadBytes = GetMaxUnreliablePayloadBytes(controllerId);

            if (routePayloadBytes <= 0) continue;

            hasViableRoute = true;
            maxPayloadBytes = Math.Min(maxPayloadBytes, routePayloadBytes);
        }

        return hasRoute && !hasViableRoute ? 0 : maxPayloadBytes;
    }

    private int GetMaxRelayPayloadBytes(string currentInstanceId, string controllerId)
    {
        var key = (currentInstanceId, controllerId);
        lock (relayPayloadBudgetGate)
        {
            if (!relayPayloadBudgets.TryGetValue(key, out int payloadBytes))
            {
                payloadBytes = CalculateMaxRelayPayloadBytes(
                    serializer,
                    currentInstanceId,
                    controllerId,
                    SafeSinglePacketBytes);
                relayPayloadBudgets[key] = payloadBytes;
                if (payloadBytes <= 0)
                {
                    Logger.Warning(
                        "[BattleTraffic] Relay framing leaves no unreliable payload capacity for " +
                        "{ControllerId} in {InstanceId}",
                        controllerId,
                        currentInstanceId);
                }
            }

            return payloadBytes;
        }
    }

    internal static int CalculateMaxRelayPayloadBytes(
        ICommonSerializer serializer,
        string instanceId,
        string controllerId,
        int maxDatagramBytes)
    {
        if (maxDatagramBytes <= 0) return 0;

        bool Fits(int payloadBytes) =>
            serializer.Serialize(new RelayPacket(
                DeliveryMethod.Unreliable,
                instanceId,
                controllerId,
                new byte[payloadBytes])).Length <= maxDatagramBytes;

        if (!Fits(0)) return 0;

        int low = 0;
        int high = maxDatagramBytes;
        while (low < high)
        {
            int candidate = low + ((high - low + 1) / 2);
            if (Fits(candidate))
                low = candidate;
            else
                high = candidate - 1;
        }

        return low;
    }

    internal static DeliveryMethod? SelectDeliveryMethod(
        IPacket packet,
        int serializedLength,
        int maxSinglePacketSize)
    {
        DeliveryMethod method = packet.DeliveryMethod;
        bool fragmentable = method == DeliveryMethod.ReliableOrdered || method == DeliveryMethod.ReliableUnordered;
        if (fragmentable || serializedLength <= Math.Min(maxSinglePacketSize, SafeSinglePacketBytes))
        {
            return method;
        }

        return IsMovementPacket(packet) ? null : DeliveryMethod.ReliableUnordered;
    }

    private static bool IsMovementPacket(IPacket packet) =>
        packet.PacketType == PacketType.Movement || packet.PacketType == PacketType.MountMovement;

    public void Send(NetPeer netPeer, IPacket packet)
    {
        Send(netPeer, packet, movementPacketCompressor.Serialize(packet));
    }

    private void Send(NetPeer netPeer, IPacket packet, byte[] data)
    {
        DeliveryMethod? selectedMethod = SelectDeliveryMethod(
            packet,
            data.Length,
            netPeer.GetMaxSinglePacketSize(packet.DeliveryMethod));
        if (!selectedMethod.HasValue) return;

        DeliveryMethod method = selectedMethod.Value;
        bool fragmentable = method == DeliveryMethod.ReliableOrdered || method == DeliveryMethod.ReliableUnordered;

        try
        {
            netPeer.Send(data, method);
        }
        catch (TooBigPacketException) when (!fragmentable)
        {
            DeliveryMethod? retryMethod = SelectDeliveryMethod(packet, data.Length, 0);
            if (retryMethod.HasValue)
                netPeer.Send(data, retryMethod.Value);
        }
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        lock (peerGate)
        {
            if (!mappedPeerControllers.ContainsKey(peer)) return;
        }

#if DEBUG
        byte[] serializedPacket = reader.GetRemainingBytes();
        var packet = serializer.Deserialize<IPacket>(serializedPacket);
        if (packet is AgentActionPacket actionPacket)
        {
            MissionActionDiagnostics.RecordActionPacketReceived(
                actionPacket,
                serializedPacket.Length);
        }
#else
        var packet = serializer.Deserialize<IPacket>(reader.GetRemainingBytes());
#endif
        packetManager.HandleReceive(peer, packet);
    }
}
