using Common.Logging;
using Common.Util;
using Serilog;
using System;
using System.Net;
using System.Net.Sockets;

namespace Common.Network.Session;

/// <summary>Exposes one provider peer as a loopback UDP endpoint.</summary>
public sealed class ProviderTunnelClient : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<ProviderTunnelClient>();

    private readonly IProviderDatagramTransport transport;
    private readonly byte[] udpBuffer = new byte[ProviderTunnel.MaxDatagramBytes];
    private readonly byte[] providerBuffer = new byte[ProviderTunnel.MaxDatagramBytes];

    private Socket socket;
    private Poller poller;
    private long connection;
    private EndPoint receiveSender = LoopbackDatagramSocket.AnyEndpoint();
    private EndPoint clientEndpoint;
    private int pendingLength;

    public ProviderTunnelClient(IProviderDatagramTransport transport)
    {
        if (transport == null) throw new ArgumentNullException(nameof(transport));

        this.transport = transport;
        transport.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public event Action Closed;

    public int LocalPort { get; private set; }

    public void Start(PlatformIdentity remoteIdentity, int channel = ProviderTunnel.SessionChannel)
    {
        if (!remoteIdentity.IsValid) throw new ArgumentException("A valid provider identity is required", nameof(remoteIdentity));

        transport.Prepare();
        socket = LoopbackDatagramSocket.Create();
        LocalPort = ((IPEndPoint)socket.LocalEndPoint).Port;
        connection = transport.Connect(remoteIdentity, channel);

        poller = new Poller(Update, ProviderTunnel.PumpInterval);
        poller.Start();

        Logger.Information(
            "{Provider} tunnel pump on 127.0.0.1:{Port} connecting to {RemoteIdentity}",
            remoteIdentity.Provider,
            LocalPort,
            remoteIdentity);
    }

    private void Update(TimeSpan _)
    {
        PumpToProvider();

        try
        {
            int size;
            while ((size = transport.Receive(connection, providerBuffer)) > 0)
            {
                if (clientEndpoint == null) continue;

                socket.SendTo(providerBuffer, size, SocketFlags.None, clientEndpoint);
            }
        }
        catch (SocketException)
        {
        }
    }

    private void PumpToProvider()
    {
        if (pendingLength > 0)
        {
            if (!transport.Send(connection, udpBuffer, pendingLength, droppable: false)) return;
            pendingLength = 0;
        }

        while (true)
        {
            int length = LoopbackDatagramSocket.TryReceiveFrom(socket, udpBuffer, ref receiveSender);
            if (length < 0) break;
            if (length == 0) continue;

            clientEndpoint = receiveSender;
            bool droppable = ProviderTunnel.IsDroppableDatagram(udpBuffer, length);
            if (!transport.Send(connection, udpBuffer, length, droppable))
            {
                pendingLength = length;
                return;
            }
        }
    }

    private void OnConnectionStateChanged(long changedConnection, ProviderConnectionState state)
    {
        if (changedConnection != connection) return;

        if (state == ProviderConnectionState.Connected)
        {
            Logger.Information("Provider tunnel established; {Status}", transport.Describe(connection));
        }
        else if (state == ProviderConnectionState.Closed)
        {
            Logger.Warning("Provider tunnel closed; {Status}", transport.Describe(connection));
            Closed?.Invoke();
        }
    }

    public void Dispose()
    {
        poller?.StopAndWait(TimeSpan.FromSeconds(1));
        transport.ConnectionStateChanged -= OnConnectionStateChanged;
        transport.Close(connection);
        transport.Dispose();
        socket?.Close();
    }
}
