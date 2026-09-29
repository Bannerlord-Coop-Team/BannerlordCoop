using Common.Logging;
using Common.Network;
using Common.Network.Coalescing;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Core.Server;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Shutdown;
using Coop.Core.Server.Services.Session.Messages;
using Coop.Core.Server.Services.Time;
using GameInterface.Services.Entity;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace Coop.Tests.Server;

/// <summary>Verifies what the server logs and publishes when it binds, or fails to bind, its UDP port.</summary>
public class CoopServerStartTests
{
    private const string ListeningPrefix = "Server listening on UDP port ";
    private const string Closing = "Players cannot join this server. Close it, free the port and start it again.";

    [Fact]
    public void Start_WhenPortHeld_LogsSocketErrorAndDoesNotListen()
    {
        var holder = new NetManager(new EventBasedNetListener());
        try
        {
            Assert.True(holder.StartInManualMode(0));
            int port = holder.LocalPort;
            using var broker = new TestMessageBroker();

            string[] logs = StartServer(port, broker, starts: 1);

            string failure = Assert.Single(logs, line => line.StartsWith($"Server failed to bind UDP port {port} "));
            Assert.StartsWith($"Server failed to bind UDP port {port} (AddressAlreadyInUse, 10048): ", failure);
            Assert.EndsWith(Closing, failure);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Contains($"): opened by this process (Windows pid {Environment.ProcessId}). ", failure);
            }
            else
            {
                Assert.Contains($"ss -ulpn 'sport = :{port}'", failure);
            }
            Assert.DoesNotContain(logs, line => line.StartsWith(ListeningPrefix));
            Assert.Empty(broker.GetMessagesFromType<ServerListening>());
        }
        finally
        {
            holder.Stop();
        }
    }

    [Fact]
    public void Start_OnFreePort_LogsListeningPortOnce()
    {
        using var broker = new TestMessageBroker();

        string[] logs = StartServer(0, broker, starts: 1);

        int port = ListeningPort(logs);
        Assert.InRange(port, 1, 65535);
        Assert.DoesNotContain(logs, line => line.StartsWith("Server failed to bind"));
        Assert.Single(broker.GetMessagesFromType<ServerListening>());
    }

    [Fact]
    public void Start_Twice_LogsAlreadyListeningNotBindFailure()
    {
        using var broker = new TestMessageBroker();

        string[] logs = StartServer(0, broker, starts: 2);

        int port = ListeningPort(logs);
        Assert.Single(logs, line => line == $"Server is already listening on UDP port {port}; ignoring the second start");
        Assert.DoesNotContain(logs, line => line.StartsWith("Server failed to bind"));
        Assert.Single(broker.GetMessagesFromType<ServerListening>());
    }

    private static int ListeningPort(string[] logs)
    {
        string listening = Assert.Single(logs, line => line.StartsWith(ListeningPrefix));
        return int.Parse(listening.Substring(ListeningPrefix.Length));
    }

    private static string[] StartServer(int port, TestMessageBroker broker, int starts)
    {
        var logs = new ConcurrentQueue<string>();
        Action<string> capture = logs.Enqueue;
        OutputSinkManager.AddLogCallback(capture);
        try
        {
            using var cancellation = new CancellationTokenSource();
            using var server = CreateServer(port, broker, cancellation);
            for (int i = 0; i < starts; i++)
            {
                server.Start();
            }
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(capture);
        }

        return logs.ToArray();
    }

    private static CoopServer CreateServer(int port, TestMessageBroker broker, CancellationTokenSource cancellation)
    {
        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.Port).Returns(port);
        config.SetupGet(value => value.DisconnectTimeout).Returns(TimeSpan.FromSeconds(5));
        config.SetupGet(value => value.UpdateTime).Returns(TimeSpan.FromMilliseconds(15));
        config.SetupGet(value => value.NetworkPollInterval).Returns(TimeSpan.FromMilliseconds(25));

        return new CoopServer(
            config.Object,
            broker,
            Mock.Of<IPacketManager>(),
            Mock.Of<IMessagePacketHandler>(),
            Mock.Of<IConnectionMessageQueue>(),
            Mock.Of<IServerAdmissionGate>(),
            Mock.Of<IJoinPeerTerminator>(),
            Mock.Of<IControllerIdProvider>(),
            Mock.Of<IMissionManager>(),
            new Lazy<IOverloadedPeerManager>(() => Mock.Of<IOverloadedPeerManager>()),
            Mock.Of<ISendCoalescer>(),
            new UdpBindDiagnostics(),
            Mock.Of<ICommonSerializer>(),
            Mock.Of<IReliableMessageBatcher<NetPeer>>(),
            cancellation);
    }
}
