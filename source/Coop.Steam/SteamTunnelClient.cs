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
    private readonly IReceivePathDiagnostics diagnostics;
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

    public SteamTunnelClient(ISteamTunnelTransport transport, IReceivePathDiagnostics diagnostics)
    {
        this.transport = transport;
        if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
        this.diagnostics = diagnostics;
        transport.ConnectionStateChanged += OnConnectionStateChanged;
    }

    /// <summary>Raised when the remote Steam connection closes unexpectedly.</summary>
    public event Action Closed;

    public int LocalPort { get; private set; }

    public void Start(ulong hostSteamId)
    {
        Start(hostSteamId, SteamTunnel.VirtualPort);
    }

    public void Start(ulong hostSteamId, int virtualPort)
    {
        transport.EnsureRelayAccess();

        socket = TunnelSocket.CreateLoopbackDatagramSocket();
        LocalPort = ((IPEndPoint)socket.LocalEndPoint).Port;

        connection = transport.ConnectToHost(hostSteamId, virtualPort);

        diagnostics.Start(Logger, $"steam-client connection={connection} remoteSteamId={hostSteamId} localRelay=127.0.0.1:{LocalPort} virtualPort={virtualPort}");
        poller = new Poller(Update, SteamTunnel.PumpInterval);
        poller.Start();

        Logger.Information("Steam tunnel pump on 127.0.0.1:{Port} connecting to host {HostSteamId}",
            LocalPort, hostSteamId.ToString());
    }

    private void Update(TimeSpan deltaTime)
    {
        PumpToSteam();

        int forwardingBytes = 0;
        try
        {
            int size;
            while ((size = transport.ReceiveDatagram(connection, steamBuffer)) > 0)
            {
                // Nothing has dialed the pump yet; the server never sends first, so drop.
                diagnostics.Record(ReceivePathEvent.SteamReceive, size);
                if (clientEndpoint == null)
                {
                    diagnostics.Record(ReceivePathEvent.NoEndpointDrop, size);
                    continue;
                }

                forwardingBytes = size;
                int sent = socket.SendTo(steamBuffer, size, SocketFlags.None, clientEndpoint);
                diagnostics.Record(sent == size ? ReceivePathEvent.UdpForwarded : ReceivePathEvent.UdpForwardFailed, sent);
            }
        }
        catch (SocketException ex)
        {
            diagnostics.Record(ReceivePathEvent.UdpForwardFailed, forwardingBytes, ex.SocketErrorCode);
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
            int length = TunnelSocket.TryReceiveFrom(socket, udpBuffer, ref receiveSender, diagnostics);
            if (length < 0) break;
            if (length == 0) continue;

            if (clientEndpoint == null)
            {
                Logger.Information("[ReceivePath] utc={Utc:O} connection={Connection} localRelayPort={LocalPort} UDP destination learned target={Target}",
                    DateTime.UtcNow, connection, LocalPort, receiveSender);
            }
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
                Closed?.Invoke();
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
        diagnostics.End("client-disposed");
    }
}
