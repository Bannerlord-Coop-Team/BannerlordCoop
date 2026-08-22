using Common.Logging;
using Common.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Common.Network.Session;

/// <summary>Forwards provider datagrams from distinct authenticated peers to one local server.</summary>
public sealed class ProviderTunnelHost : ISessionTunnelHost, IAuthenticatedPeerIdentityResolver
{
    private static readonly ILogger Logger = LogManager.GetLogger<ProviderTunnelHost>();

    private sealed class TunnelPeer
    {
        public Socket Socket;
        public IPEndPoint ServerPeerEndpoint;
        public PlatformIdentity RemoteIdentity;
        public byte[] PendingDatagram = new byte[ProviderTunnel.MaxDatagramBytes];
        public int PendingLength;
    }

    private readonly IProviderDatagramTransport transport;
    private readonly IPeerIdentityPublisher identityPublisher;
    private readonly int channel;
    private readonly object gate = new object();
    private readonly HashSet<long> connectingConnections = new HashSet<long>();
    private readonly Dictionary<long, TunnelPeer> peers = new Dictionary<long, TunnelPeer>();
    private readonly Dictionary<IPEndPoint, PlatformIdentity> remoteIdentities =
        new Dictionary<IPEndPoint, PlatformIdentity>();
    private readonly byte[] serverBuffer = new byte[ProviderTunnel.MaxDatagramBytes];
    private readonly byte[] providerBuffer = new byte[ProviderTunnel.MaxDatagramBytes];

    private volatile KeyValuePair<long, TunnelPeer>[] peerSnapshot =
        Array.Empty<KeyValuePair<long, TunnelPeer>>();
    private EndPoint receiveSender = LoopbackDatagramSocket.AnyEndpoint();
    private IPEndPoint serverEndpoint;
    private Poller poller;
    private volatile bool listening;

    public ProviderTunnelHost(
        IProviderDatagramTransport transport,
        int channel = ProviderTunnel.SessionChannel,
        IPeerIdentityPublisher identityPublisher = null)
    {
        if (transport == null) throw new ArgumentNullException(nameof(transport));

        this.transport = transport;
        this.channel = channel;
        this.identityPublisher = identityPublisher ?? NoopPeerIdentityPublisher.Instance;
        transport.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public event Action<PlatformIdentity> PeerDisconnected;

    public bool IsListening => listening;
    public int PeerCount => peerSnapshot.Length;

    public bool TryGetIdentity(IPEndPoint serverPeerEndpoint, out PlatformIdentity identity)
    {
        lock (gate)
        {
            if (serverPeerEndpoint != null)
                return remoteIdentities.TryGetValue(serverPeerEndpoint, out identity);

            identity = default;
            return false;
        }
    }

    public void Start(int serverPort)
    {
        if (listening) return;

        serverEndpoint = new IPEndPoint(IPAddress.Loopback, serverPort);
        transport.Prepare();
        transport.Listen(channel);

        poller = new Poller(Update, ProviderTunnel.PumpInterval);
        poller.Start();
        listening = true;

        Logger.Information(
            "{Provider} tunnel listening on channel {Channel}; forwarding peers to {ServerEndpoint}",
            transport.LocalIdentity.Provider,
            channel,
            serverEndpoint);
    }

    public void ClosePeer(PlatformIdentity remoteIdentity)
    {
        if (!remoteIdentity.IsValid) return;

        long connection = 0;
        TunnelPeer peer = null;
        lock (gate)
        {
            foreach (var pair in peers)
            {
                if (pair.Value.RemoteIdentity != remoteIdentity) continue;
                connection = pair.Key;
                peer = pair.Value;
                break;
            }

            if (peer != null) RemovePeerLocked(connection, peer);
        }

        if (peer == null) return;
        transport.Close(connection);
        peer.Socket.Close();
    }

    public void Stop()
    {
        if (!listening) return;
        listening = false;

        transport.StopListening();
        poller?.StopAndWait(TimeSpan.FromSeconds(1));

        KeyValuePair<long, TunnelPeer>[] remaining;
        long[] pending;
        lock (gate)
        {
            remaining = peers.ToArray();
            pending = connectingConnections.ToArray();
            peers.Clear();
            connectingConnections.Clear();
            remoteIdentities.Clear();
            peerSnapshot = Array.Empty<KeyValuePair<long, TunnelPeer>>();
        }

        foreach (long connection in pending) transport.Close(connection);
        identityPublisher.UnregisterAll();
        foreach (var pair in remaining)
        {
            transport.Close(pair.Key);
            pair.Value.Socket.Close();
        }
    }

    private void OnConnectionStateChanged(long connection, ProviderConnectionState state)
    {
        switch (state)
        {
            case ProviderConnectionState.Connecting:
                bool acceptConnection;
                lock (gate)
                {
                    acceptConnection = listening;
                    if (acceptConnection) connectingConnections.Add(connection);
                }

                if (acceptConnection)
                    transport.Accept(connection);
                else
                    transport.Close(connection);
                break;

            case ProviderConnectionState.Connected:
                bool closeLateConnection = false;
                lock (gate)
                {
                    if (peers.ContainsKey(connection)) return;

                    bool wasAccepted = connectingConnections.Remove(connection);
                    if (!listening || !wasAccepted)
                    {
                        closeLateConnection = true;
                    }
                    else if (!transport.TryGetRemoteIdentity(connection, out var identity) ||
                        !identity.IsValid)
                    {
                        closeLateConnection = true;
                        Logger.Warning(
                            "Provider tunnel rejected connection {Connection} without an authenticated identity",
                            connection);
                    }
                    else
                    {
                        var socket = LoopbackDatagramSocket.Create();
                        socket.Connect(serverEndpoint);
                        var serverPeerEndpoint = (IPEndPoint)socket.LocalEndPoint;
                        if (!identityPublisher.TryRegister(serverPeerEndpoint, identity))
                        {
                            socket.Close();
                            closeLateConnection = true;
                            Logger.Warning(
                                "Provider tunnel rejected peer {Identity}; the authoritative server could not bind its identity",
                                identity);
                        }
                        else
                        {
                            var peer = new TunnelPeer
                            {
                                Socket = socket,
                                ServerPeerEndpoint = serverPeerEndpoint,
                                RemoteIdentity = identity,
                            };
                            peers[connection] = peer;
                            remoteIdentities[serverPeerEndpoint] = identity;
                            peerSnapshot = peers.ToArray();

                            Logger.Information(
                                "Tunnel peer {Connection} ({Identity}) connected; local relay port {Port}; {Status}",
                                connection,
                                identity,
                                serverPeerEndpoint.Port,
                                transport.Describe(connection));
                        }
                    }
                }

                if (closeLateConnection) transport.Close(connection);
                break;

            case ProviderConnectionState.Closed:
                TunnelPeer closedPeer;
                lock (gate)
                {
                    connectingConnections.Remove(connection);
                    if (!peers.TryGetValue(connection, out closedPeer)) return;
                    RemovePeerLocked(connection, closedPeer);
                }

                closedPeer.Socket.Close();
                if (closedPeer.RemoteIdentity.IsValid)
                    PeerDisconnected?.Invoke(closedPeer.RemoteIdentity);
                break;
        }
    }

    private void RemovePeerLocked(long connection, TunnelPeer peer)
    {
        peers.Remove(connection);
        remoteIdentities.Remove(peer.ServerPeerEndpoint);
        peerSnapshot = peers.ToArray();
        identityPublisher.Unregister(peer.ServerPeerEndpoint);
    }

    private void Update(TimeSpan _)
    {
        foreach (var pair in peerSnapshot)
        {
            try
            {
                PumpToProvider(pair.Key, pair.Value);

                int size;
                while ((size = transport.Receive(pair.Key, providerBuffer)) > 0)
                {
                    pair.Value.Socket.Send(providerBuffer, size, SocketFlags.None);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }
    }

    private void PumpToProvider(long connection, TunnelPeer peer)
    {
        if (peer.PendingLength > 0)
        {
            if (!transport.Send(connection, peer.PendingDatagram, peer.PendingLength, droppable: false)) return;
            peer.PendingLength = 0;
        }

        int length;
        while ((length = LoopbackDatagramSocket.TryReceiveFrom(peer.Socket, serverBuffer, ref receiveSender)) >= 0)
        {
            if (length == 0) continue;

            bool droppable = ProviderTunnel.IsDroppableDatagram(serverBuffer, length);
            if (!transport.Send(connection, serverBuffer, length, droppable))
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
