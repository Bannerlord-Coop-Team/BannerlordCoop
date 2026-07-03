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

    private readonly ISteamTunnelTransport transport;
    private readonly object gate = new object();
    private readonly Dictionary<uint, Socket> peerSockets = new Dictionary<uint, Socket>();
    private readonly byte[] serverBuffer = new byte[SteamTunnel.MaxDatagramBytes];
    private readonly byte[] steamBuffer = new byte[SteamTunnel.MaxDatagramBytes];

    // Rebuilt under the gate whenever the peer set changes, so the 2ms pump tick reads a
    // stable array without taking the lock.
    private volatile KeyValuePair<uint, Socket>[] peerSnapshot = Array.Empty<KeyValuePair<uint, Socket>>();
    // Only touched on the poller thread.
    private EndPoint receiveSender = TunnelSocket.AnyEndpoint();

    private IPEndPoint serverEndpoint;
    private Poller poller;
    private volatile bool listening;

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

        KeyValuePair<uint, Socket>[] peers;
        lock (gate)
        {
            peers = peerSockets.ToArray();
            peerSockets.Clear();
            peerSnapshot = Array.Empty<KeyValuePair<uint, Socket>>();
        }

        foreach (var peer in peers)
        {
            transport.CloseConnection(peer.Key);
            peer.Value.Close();
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
                    if (!listening || peerSockets.ContainsKey(connection)) return;

                    var socket = TunnelSocket.CreateLoopbackDatagramSocket();
                    socket.Connect(serverEndpoint);
                    peerSockets[connection] = socket;
                    peerSnapshot = peerSockets.ToArray();

                    Logger.Information("Tunnel peer {Connection} connected; local relay port {Port}",
                        connection, ((IPEndPoint)socket.LocalEndPoint).Port);
                }
                break;

            case TunnelConnectionState.Closed:
                Socket peerSocket;
                lock (gate)
                {
                    if (!peerSockets.TryGetValue(connection, out peerSocket)) return;

                    peerSockets.Remove(connection);
                    peerSnapshot = peerSockets.ToArray();
                }

                peerSocket.Close();
                Logger.Information("Tunnel peer {Connection} disconnected", connection);
                break;
        }
    }

    private void Update(TimeSpan deltaTime)
    {
        foreach (var peer in peerSnapshot)
        {
            try
            {
                int length;
                while ((length = TunnelSocket.TryReceiveFrom(peer.Value, serverBuffer, ref receiveSender)) >= 0)
                {
                    if (length == 0) continue;

                    transport.SendDatagram(peer.Key, serverBuffer, length);
                }

                int size;
                while ((size = transport.ReceiveDatagram(peer.Key, steamBuffer)) > 0)
                {
                    peer.Value.Send(steamBuffer, size, SocketFlags.None);
                }
            }
            catch (ObjectDisposedException)
            {
                // The Closed handler disposed this peer's socket mid-tick; the next tick's
                // snapshot no longer contains it.
            }
        }
    }

    public void Dispose()
    {
        Stop();
        transport.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
