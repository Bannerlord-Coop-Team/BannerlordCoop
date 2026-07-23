using Common.Logging;
using Common.Network.Session;
using Common.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Coop.Steam;

/// <inheritdoc cref="ISessionTunnelHost"/>
/// <remarks>
/// Gives every accepted Steam peer a stable, distinct loopback UDP endpoint because LiteNetLib
/// keys peers by endpoint and would merge clients sharing one socket.
/// </remarks>
public class SteamTunnelHost : ISessionTunnelHost, ISessionTunnelIdentityResolver
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamTunnelHost>();

    // A peer's loopback socket toward the server, plus one held-back datagram for when the
    // Steam send buffer is full: the pump retries it before draining the socket further.
    private sealed class TunnelPeer
    {
        public Socket Socket;
        public IPEndPoint ServerPeerEndpoint;
        public ulong RemoteSteamId;
        public byte[] PendingDatagram = new byte[SteamTunnel.MaxDatagramBytes];
        public int PendingLength;
    }

    private readonly ISteamTunnelTransport transport;
    private readonly object gate = new object();
    private readonly HashSet<uint> connectingConnections = new HashSet<uint>();
    private readonly Dictionary<uint, TunnelPeer> peers = new Dictionary<uint, TunnelPeer>();
    private readonly Dictionary<IPEndPoint, ulong> remoteSteamIds = new Dictionary<IPEndPoint, ulong>();
    private readonly byte[] serverBuffer = new byte[SteamTunnel.MaxDatagramBytes];
    private readonly byte[] steamBuffer = new byte[SteamTunnel.MaxDatagramBytes];

    // Rebuilt under the gate whenever the peer set changes, so the 2ms pump tick reads a
    // stable array without taking the lock.
    private volatile KeyValuePair<uint, TunnelPeer>[] peerSnapshot = Array.Empty<KeyValuePair<uint, TunnelPeer>>();
    // Only touched on the poller thread.
    private EndPoint receiveSender = TunnelSocket.AnyEndpoint();

    private IPEndPoint serverEndpoint;
    private Poller poller;
    private volatile bool listening;
    // ~10s of 2ms pump ticks between connection-stats log lines.
    private const int StatsLogTicks = 5000;
    private int ticksSinceStatsLog;

    public SteamTunnelHost(ISteamTunnelTransport transport)
    {
        this.transport = transport;
        transport.ConnectionStateChanged += OnConnectionStateChanged;
    }

    /// <summary>Raised when an established remote Steam peer closes unexpectedly.</summary>
    public event Action<ulong> PeerDisconnected;

    public bool IsListening => listening;

    public int PeerCount => peerSnapshot.Length;

    public bool TryGetRemoteSteamId(IPEndPoint serverPeerEndpoint, out ulong steamId)
    {
        lock (gate)
        {
            if (serverPeerEndpoint != null)
            {
                return remoteSteamIds.TryGetValue(serverPeerEndpoint, out steamId);
            }

            steamId = 0;
            return false;
        }
    }

    public void Start(int serverPort)
    {
        Start(serverPort, SteamTunnel.VirtualPort);
    }

    public void Start(int serverPort, int virtualPort)
    {
        if (listening) return;

        serverEndpoint = new IPEndPoint(IPAddress.Loopback, serverPort);
        transport.EnsureRelayAccess();
        transport.ListenForClients(virtualPort);

        poller = new Poller(Update, SteamTunnel.PumpInterval);
        poller.Start();
        listening = true;

        Logger.Information("Steam tunnel host listening on virtual port {VirtualPort}; forwarding peers to {ServerEndpoint}",
            virtualPort, serverEndpoint);
    }

    public void ClosePeer(ulong remoteSteamId)
    {
        if (remoteSteamId == 0) return;

        uint connection = 0;
        TunnelPeer peer = null;
        lock (gate)
        {
            foreach (var pair in peers)
            {
                if (pair.Value.RemoteSteamId != remoteSteamId) continue;
                connection = pair.Key;
                peer = pair.Value;
                break;
            }

            if (peer != null) RemovePeerLocked(connection, peer);
        }

        if (peer == null) return;
        transport.CloseConnection(connection);
        peer.Socket.Close();
    }

    public void Stop()
    {
        if (!listening) return;
        listening = false;

        transport.StopListening();
        // Wait out any in-flight pump tick so the socket teardown below can't race it.
        poller?.StopAndWait(TimeSpan.FromSeconds(1));

        KeyValuePair<uint, TunnelPeer>[] remaining;
        uint[] pending;
        lock (gate)
        {
            remaining = peers.ToArray();
            pending = connectingConnections.ToArray();
            peers.Clear();
            connectingConnections.Clear();
            remoteSteamIds.Clear();
            peerSnapshot = Array.Empty<KeyValuePair<uint, TunnelPeer>>();
        }

        foreach (var connection in pending) transport.CloseConnection(connection);
        foreach (var peer in remaining)
        {
            transport.CloseConnection(peer.Key);
            peer.Value.Socket.Close();
        }

        Logger.Information("Steam tunnel host stopped");
    }

    private void OnConnectionStateChanged(uint connection, TunnelConnectionState state)
    {
        switch (state)
        {
            case TunnelConnectionState.Connecting:
                bool acceptConnection;
                lock (gate)
                {
                    acceptConnection = listening;
                    if (acceptConnection) connectingConnections.Add(connection);
                }

                if (acceptConnection)
                {
                    transport.AcceptConnection(connection);
                }
                else
                {
                    transport.CloseConnection(connection);
                }
                break;

            case TunnelConnectionState.Connected:
                bool closeLateConnection = false;
                lock (gate)
                {
                    if (peers.ContainsKey(connection)) return;

                    bool wasAccepted = connectingConnections.Remove(connection);
                    if (!listening || !wasAccepted)
                    {
                        closeLateConnection = true;
                    }

                    if (!closeLateConnection)
                    {
                        var socket = TunnelSocket.CreateLoopbackDatagramSocket();
                        socket.Connect(serverEndpoint);
                        var serverPeerEndpoint = (IPEndPoint)socket.LocalEndPoint;
                        ulong steamId = 0;
                        if (transport is ISteamTunnelConnectionIdentityResolver identityResolver)
                        {
                            identityResolver.TryGetRemoteSteamId(connection, out steamId);
                        }
                        peers[connection] = new TunnelPeer
                        {
                            Socket = socket,
                            ServerPeerEndpoint = serverPeerEndpoint,
                            RemoteSteamId = steamId,
                        };
                        if (steamId != 0)
                        {
                            remoteSteamIds[serverPeerEndpoint] = steamId;
                        }
                        peerSnapshot = peers.ToArray();

                        // The effective sendRate/buffer here also proves whether the listen
                        // socket's config options reached the accepted connection.
                        Logger.Information("Tunnel peer {Connection} connected; local relay port {Port}; {Status}",
                            connection, serverPeerEndpoint.Port, transport.DescribeConnection(connection));
                    }
                }

                if (closeLateConnection) transport.CloseConnection(connection);
                break;

            case TunnelConnectionState.Closed:
                TunnelPeer closedPeer;
                lock (gate)
                {
                    connectingConnections.Remove(connection);
                    if (!peers.TryGetValue(connection, out closedPeer)) return;
                    RemovePeerLocked(connection, closedPeer);
                }

                closedPeer.Socket.Close();
                Logger.Information("Tunnel peer {Connection} disconnected", connection);
                if (closedPeer.RemoteSteamId != 0) PeerDisconnected?.Invoke(closedPeer.RemoteSteamId);
                break;
        }
    }

    private void RemovePeerLocked(uint connection, TunnelPeer peer)
    {
        peers.Remove(connection);
        remoteSteamIds.Remove(peer.ServerPeerEndpoint);
        peerSnapshot = peers.ToArray();
    }

    private void Update(TimeSpan deltaTime)
    {
        if (peerSnapshot.Length > 0 && ++ticksSinceStatsLog >= StatsLogTicks)
        {
            ticksSinceStatsLog = 0;
            foreach (var peer in peerSnapshot)
            {
                Logger.Information("Tunnel peer {Connection}: {Status}",
                    peer.Key, transport.DescribeConnection(peer.Key));
            }
        }

        foreach (var peer in peerSnapshot)
        {
            try
            {
                PumpToSteam(peer.Key, peer.Value);

                int size;
                while ((size = transport.ReceiveDatagram(peer.Key, steamBuffer)) > 0)
                {
                    peer.Value.Socket.Send(steamBuffer, size, SocketFlags.None);
                }
            }
            catch (ObjectDisposedException)
            {
                // The Closed handler disposed this peer's socket mid-tick; the next tick's
                // snapshot no longer contains it.
            }
            catch (SocketException)
            {
                // A refused loopback send loses one datagram; LiteNetLib retransmits what matters.
            }
        }
    }

    // A refused send parks the datagram and stops draining, so the OS socket buffer queues
    // the rest instead of anything reliable being dropped while the Steam send buffer is full.
    private void PumpToSteam(uint connection, TunnelPeer peer)
    {
        // Only reliable-class datagrams ever park, so the retry is never droppable.
        if (peer.PendingLength > 0)
        {
            if (!transport.SendDatagram(connection, peer.PendingDatagram, peer.PendingLength, droppable: false)) return;

            peer.PendingLength = 0;
        }

        int length;
        while ((length = TunnelSocket.TryReceiveFrom(peer.Socket, serverBuffer, ref receiveSender)) >= 0)
        {
            if (length == 0) continue;

            bool droppable = SteamTunnel.IsDroppableDatagram(serverBuffer, length);
            if (!transport.SendDatagram(connection, serverBuffer, length, droppable))
            {
                Array.Copy(serverBuffer, peer.PendingDatagram, length);
                peer.PendingLength = length;
                return;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        transport.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
