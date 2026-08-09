using Common.Network.Session;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Xunit;

namespace Common.Tests.Network.Session;

public class ProviderTunnelClientTests
{
    [Fact]
    public void Start_BindsLoopbackAndUsesProviderContract()
    {
        var transport = new FakeProviderDatagramTransport();
        using var client = new ProviderTunnelClient(transport);
        var remote = new PlatformIdentity("gog", "42");

        client.Start(remote, ProviderTunnel.MissionChannel);

        Assert.NotEqual(0, client.LocalPort);
        Assert.Equal(remote, transport.ConnectedIdentity);
        Assert.Equal(ProviderTunnel.MissionChannel, transport.ConnectedChannel);
        Assert.Equal(1, transport.PrepareCalls);
    }

    [Fact]
    public void Datagrams_FlowBothWaysThroughLoopbackPump()
    {
        var transport = new FakeProviderDatagramTransport();
        using var client = new ProviderTunnelClient(transport);
        client.Start(new PlatformIdentity("gog", "42"));

        using var liteNetSocket = CreateSocket();
        var pumpEndpoint = new IPEndPoint(IPAddress.Loopback, client.LocalPort);
        byte[] outbound = { 1, 2, 3 };
        liteNetSocket.SendTo(outbound, pumpEndpoint);

        WaitUntil(() => transport.SentDatagrams.Length == 1);
        Assert.Equal(outbound, transport.SentDatagrams[0].Data);

        byte[] inbound = { 9, 8, 7 };
        transport.EnqueueReceive(transport.NextConnection, inbound);
        var buffer = new byte[ProviderTunnel.MaxDatagramBytes];
        int received = liteNetSocket.Receive(buffer);
        Assert.Equal(inbound, buffer.Take(received).ToArray());
    }

    [Fact]
    public void ReliableBackpressure_PreservesOrderWhileDroppableDataIsDiscarded()
    {
        var transport = new FakeProviderDatagramTransport();
        using var client = new ProviderTunnelClient(transport);
        client.Start(new PlatformIdentity("steam", "42"));
        using var liteNetSocket = CreateSocket();
        var pumpEndpoint = new IPEndPoint(IPAddress.Loopback, client.LocalPort);

        transport.FailSendsRemaining = 1;
        liteNetSocket.SendTo(new byte[] { 0, 9 }, pumpEndpoint);
        WaitUntil(() => transport.RejectedSends == 1);
        liteNetSocket.SendTo(new byte[] { 1, 7 }, pumpEndpoint);
        liteNetSocket.SendTo(new byte[] { 1, 8 }, pumpEndpoint);

        WaitUntil(() => transport.SentDatagrams.Length == 2);
        Assert.Equal(new byte[] { 1, 7 }, transport.SentDatagrams[0].Data);
        Assert.Equal(new byte[] { 1, 8 }, transport.SentDatagrams[1].Data);
    }

    internal static void WaitUntil(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "Tunnel pump did not forward in time");
            Thread.Sleep(10);
        }
    }

    internal static Socket CreateSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        socket.ReceiveTimeout = 5000;
        return socket;
    }
}

public class ProviderTunnelHostTests
{
    [Fact]
    public void ConnectedPeer_RequiresAndExposesAuthenticatedIdentity()
    {
        var transport = new FakeProviderDatagramTransport();
        var identityPublisher = new RecordingPeerIdentityPublisher();
        using var host = new ProviderTunnelHost(
            transport,
            identityPublisher: identityPublisher);
        using var serverSocket = ProviderTunnelClientTests.CreateSocket();
        int serverPort = ((IPEndPoint)serverSocket.LocalEndPoint).Port;
        var remote = new PlatformIdentity("gog", "42");

        host.Start(serverPort);
        transport.SetRemoteIdentity(7, remote);
        transport.RaiseConnectionState(7, ProviderConnectionState.Connecting);
        transport.RaiseConnectionState(7, ProviderConnectionState.Connected);
        transport.EnqueueReceive(7, new byte[] { 1 });

        var buffer = new byte[ProviderTunnel.MaxDatagramBytes];
        EndPoint relayEndpoint = new IPEndPoint(IPAddress.Any, 0);
        serverSocket.ReceiveFrom(buffer, ref relayEndpoint);

        Assert.True(host.TryGetIdentity((IPEndPoint)relayEndpoint, out var actual));
        Assert.Equal(remote, actual);
        Assert.Equal(remote, identityPublisher.RegisteredIdentity);
        Assert.Equal(relayEndpoint, identityPublisher.RegisteredEndpoint);
    }

    [Fact]
    public void ConnectedPeer_WhenAuthoritativeIdentityBindingFails_IsClosed()
    {
        var transport = new FakeProviderDatagramTransport();
        var identityPublisher = new RecordingPeerIdentityPublisher { RegistrationSucceeds = false };
        using var host = new ProviderTunnelHost(
            transport,
            identityPublisher: identityPublisher);
        using var serverSocket = ProviderTunnelClientTests.CreateSocket();

        host.Start(((IPEndPoint)serverSocket.LocalEndPoint).Port);
        Connect(transport, 7, new PlatformIdentity("gog", "42"));

        Assert.Equal(0, host.PeerCount);
        Assert.Contains(7, transport.ClosedConnections);
    }

    [Fact]
    public void ConnectedPeerWithoutAuthenticatedIdentity_IsClosed()
    {
        var transport = new FakeProviderDatagramTransport();
        using var host = new ProviderTunnelHost(transport);
        using var serverSocket = ProviderTunnelClientTests.CreateSocket();

        host.Start(((IPEndPoint)serverSocket.LocalEndPoint).Port);
        transport.RaiseConnectionState(7, ProviderConnectionState.Connecting);
        transport.RaiseConnectionState(7, ProviderConnectionState.Connected);

        Assert.Equal(0, host.PeerCount);
        Assert.Contains(7, transport.ClosedConnections);
    }

    [Fact]
    public void Datagrams_FlowBothWaysAndUseDistinctRelayEndpoint()
    {
        var transport = new FakeProviderDatagramTransport();
        using var host = new ProviderTunnelHost(transport);
        using var serverSocket = ProviderTunnelClientTests.CreateSocket();
        int serverPort = ((IPEndPoint)serverSocket.LocalEndPoint).Port;
        host.Start(serverPort);
        Connect(transport, 7, new PlatformIdentity("steam", "7"));
        Connect(transport, 8, new PlatformIdentity("steam", "8"));

        transport.EnqueueReceive(7, new byte[] { 7 });
        transport.EnqueueReceive(8, new byte[] { 8 });
        var buffer = new byte[ProviderTunnel.MaxDatagramBytes];
        EndPoint first = new IPEndPoint(IPAddress.Any, 0);
        EndPoint second = new IPEndPoint(IPAddress.Any, 0);
        serverSocket.ReceiveFrom(buffer, ref first);
        serverSocket.ReceiveFrom(buffer, ref second);
        Assert.NotEqual(((IPEndPoint)first).Port, ((IPEndPoint)second).Port);

        serverSocket.SendTo(new byte[] { 4, 5 }, first);
        ProviderTunnelClientTests.WaitUntil(() => transport.SentDatagrams.Length == 1);
        Assert.Equal(new byte[] { 4, 5 }, transport.SentDatagrams[0].Data);
    }

    private static void Connect(
        FakeProviderDatagramTransport transport,
        long connection,
        PlatformIdentity identity)
    {
        transport.SetRemoteIdentity(connection, identity);
        transport.RaiseConnectionState(connection, ProviderConnectionState.Connecting);
        transport.RaiseConnectionState(connection, ProviderConnectionState.Connected);
    }

    private sealed class RecordingPeerIdentityPublisher : IPeerIdentityPublisher
    {
        public bool RegistrationSucceeds { get; set; } = true;
        public bool IsAvailable => true;
        public IPEndPoint RegisteredEndpoint { get; private set; }
        public PlatformIdentity RegisteredIdentity { get; private set; }

        public bool TryRegister(IPEndPoint serverPeerEndpoint, PlatformIdentity identity)
        {
            RegisteredEndpoint = serverPeerEndpoint;
            RegisteredIdentity = identity;
            return RegistrationSucceeds;
        }

        public void Unregister(IPEndPoint serverPeerEndpoint) { }
        public void UnregisterAll() { }
        public void Dispose() { }
    }
}

public class ProviderMissionPeerTransportTests
{
    [Fact]
    public void ClosedSubscriberThrows_StillSchedulesOutgoingClientDisposal()
    {
        var hostTransport = new FakeProviderDatagramTransport
        {
            LocalIdentity = new PlatformIdentity("gog", "2"),
        };
        var clientTransport = new FakeProviderDatagramTransport
        {
            LocalIdentity = new PlatformIdentity("gog", "2"),
        };
        Action cleanup = null;
        using var mission = new ProviderMissionPeerTransport(
            hostTransport,
            () => clientTransport,
            scheduledCleanup => cleanup = scheduledCleanup);

        mission.Start(4200);
        Assert.True(mission.TryConnect(new PlatformIdentity("gog", "1"), out _));
        mission.PeerDisconnected += _ => throw new InvalidOperationException("scripted subscriber failure");

        Assert.Throws<InvalidOperationException>(() =>
            clientTransport.RaiseConnectionState(
                clientTransport.NextConnection,
                ProviderConnectionState.Closed));

        Assert.NotNull(cleanup);
        cleanup();
        Assert.True(clientTransport.Disposed);
        Assert.Contains(clientTransport.NextConnection, clientTransport.ClosedConnections);
    }
}
