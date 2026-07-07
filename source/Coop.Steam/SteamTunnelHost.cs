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
/// Runs in the hosting player's client. Each accepted Steam peer gets its own loopback UDP
/// socket toward the local server, because LiteNetLib keys peers by endpoint: a shared
/// socket would merge every tunneled client into one peer, and the per-peer port must stay
/// stable for the session (AllowPeerAddressChange is off).
/// </remarks>
public class SteamTunnelHost : ISessionTunnelHost
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamTunnelHost>();

    // A peer's loopback socket toward the server, plus one held-back datagram for when the
    // Steam send buffer is full: the pump retries it before draining the socket further.
    private sealed class TunnelPeer
    {
        public Socket Socket;
        public byte[] PendingDatagram = new byte[SteamTunnel.MaxDatagramBytes];
        public int PendingLength;
    }

    private readonly ISteamTunnelTransport transport;
    private readonly object gate = new object();
    private readonly Dictionary<uint, TunnelPeer> peers = new Dictionary<uint, TunnelPeer>();
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

    public bool IsListening => listening;

    public int PeerCount => peerSnapshot.Length;

    public void Start(int serverPort)
    {
        if (listening) return;

        serverEndpoint = new IPEndPoint(IPAddress.Loopback, serverPort);
        transport.EnsureRelayAccess();
        transport.ListenForClients(SteamTunnel.VirtualPort);

        poller = new Poller(Update, SteamTunnel.PumpInterval);
        poller.Start();
        listening = true;

        Logger.Information("Steam tunnel host listening; forwarding peers to {ServerEndpoint}", serverEndpoint);
    }

    public void Stop()
    {
        if (!listening) return;
        listening = false;

        transport.StopListening();
        // Wait out any in-flight pump tick so the socket teardown below can't race it.
        poller?.StopAndWait(TimeSpan.FromSeconds(1));

        KeyValuePair<uint, TunnelPeer>[] remaining;
        lock (gate)
        {
            remaining = peers.ToArray();
            peers.Clear();
            peerSnapshot = Array.Empty<KeyValuePair<uint, TunnelPeer>>();
        }

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
                if (!listening) return;

                transport.AcceptConnection(connection);
                break;

            case TunnelConnectionState.Connected:
                lock (gate)
                {
                    if (!listening || peers.ContainsKey(connection)) return;

                    var socket = TunnelSocket.CreateLoopbackDatagramSocket();
                    socket.Connect(serverEndpoint);
                    peers[connection] = new TunnelPeer { Socket = socket };
                    peerSnapshot = peers.ToArray();

                    // The effective sendRate/buffer here also proves whether the listen
                    // socket's config options reached the accepted connection.
                    Logger.Information("Tunnel peer {Connection} connected; local relay port {Port}; {Status}",
                        connection, ((IPEndPoint)socket.LocalEndPoint).Port, transport.DescribeConnection(connection));
                }
                break;

            case TunnelConnectionState.Closed:
                TunnelPeer closedPeer;
                lock (gate)
                {
                    if (!peers.TryGetValue(connection, out closedPeer)) return;

                    peers.Remove(connection);
                    peerSnapshot = peers.ToArray();
                }

                closedPeer.Socket.Close();
                Logger.Information("Tunnel peer {Connection} disconnected", connection);
                break;
        }
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
