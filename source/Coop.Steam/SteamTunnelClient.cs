using Common.Logging;
using Common.Util;
using Serilog;
using System;
using System.Net;
using System.Net.Sockets;

namespace Coop.Steam;

/// <summary>
/// Joiner-side tunnel pump: a loopback UDP socket the local LiteNetLib client dials, with
/// every datagram forwarded over a Steam P2P connection to the hosting player and back.
/// </summary>
public class SteamTunnelClient : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamTunnelClient>();

    private readonly ISteamTunnelTransport transport;
    private readonly byte[] udpBuffer = new byte[SteamTunnel.MaxDatagramBytes];
    private readonly byte[] steamBuffer = new byte[SteamTunnel.MaxDatagramBytes];

    private Socket socket;
    private Poller poller;
    private uint connection;
    // Both endpoints are only touched on the poller thread; clientEndpoint is learned from
    // the first datagram.
    private EndPoint receiveSender = TunnelSocket.AnyEndpoint();
    private EndPoint clientEndpoint;
    // Length of a held-back datagram for when the Steam send buffer is full. It stays in
    // udpBuffer, which nothing overwrites until the retry succeeds.
    private int pendingLength;

    public SteamTunnelClient(ISteamTunnelTransport transport)
    {
        this.transport = transport;
        transport.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public int LocalPort { get; private set; }

    public void Start(ulong hostSteamId)
    {
        transport.EnsureRelayAccess();

        socket = TunnelSocket.CreateLoopbackDatagramSocket();
        LocalPort = ((IPEndPoint)socket.LocalEndPoint).Port;

        connection = transport.ConnectToHost(hostSteamId, SteamTunnel.VirtualPort);

        poller = new Poller(Update, SteamTunnel.PumpInterval);
        poller.Start();

        Logger.Information("Steam tunnel pump on 127.0.0.1:{Port} connecting to host {HostSteamId}",
            LocalPort, hostSteamId.ToString());
    }

    private void Update(TimeSpan deltaTime)
    {
        PumpToSteam();

        try
        {
            int size;
            while ((size = transport.ReceiveDatagram(connection, steamBuffer)) > 0)
            {
                // Nothing has dialed the pump yet; the server never sends first, so drop.
                if (clientEndpoint == null) continue;

                socket.SendTo(steamBuffer, size, SocketFlags.None, clientEndpoint);
            }
        }
        catch (SocketException)
        {
            // A refused loopback send loses one datagram; LiteNetLib retransmits what matters.
        }
    }

    // A refused send parks the datagram and stops draining, so the OS socket buffer queues
    // the rest instead of anything being dropped while the Steam send buffer is full.
    private void PumpToSteam()
    {
        // Only reliable-class datagrams ever park, so the retry is never droppable.
        if (pendingLength > 0)
        {
            if (!transport.SendDatagram(connection, udpBuffer, pendingLength, droppable: false)) return;

            pendingLength = 0;
        }

        while (true)
        {
            int length = TunnelSocket.TryReceiveFrom(socket, udpBuffer, ref receiveSender);
            if (length < 0) break;
            if (length == 0) continue;

            clientEndpoint = receiveSender;
            bool droppable = SteamTunnel.IsDroppableDatagram(udpBuffer, length);
            if (!transport.SendDatagram(connection, udpBuffer, length, droppable))
            {
                pendingLength = length;
                return;
            }
        }
    }

    private void OnConnectionStateChanged(uint changedConnection, TunnelConnectionState state)
    {
        if (changedConnection != connection) return;

        switch (state)
        {
            case TunnelConnectionState.Connected:
                Logger.Information("Steam tunnel to the host established; {Status}",
                    transport.DescribeConnection(connection));
                break;
            case TunnelConnectionState.Closed:
                Logger.Warning("Steam tunnel to the host closed; {Status}",
                    transport.DescribeConnection(connection));
                break;
        }
    }

    public void Dispose()
    {
        // Wait out any in-flight pump tick so the teardown below can't race it.
        poller?.StopAndWait(TimeSpan.FromSeconds(1));
        transport.ConnectionStateChanged -= OnConnectionStateChanged;
        transport.CloseConnection(connection);
        transport.Dispose();
        socket?.Close();
    }
}
