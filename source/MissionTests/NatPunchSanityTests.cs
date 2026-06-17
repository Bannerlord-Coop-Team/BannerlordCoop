using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Missions.Services.Network;
using GameInterface.Missions.Services.Network.Messages;
using GameInterface.Services.Entity;
using IntroServer.Config;
using IntroServer.Server;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace MissionTests
{
    /// <summary>
    /// End-to-end checks for NAT hole punching on loopback. Spins up the real
    /// <see cref="MissionTestServer"/> (rendezvous/intro server) and two real
    /// <see cref="LiteNetP2PClient"/> instances over loopback UDP — i.e. "two game instances on one
    /// machine" exactly, just in one process. Used to verify the P2P transport behaviours that can't be
    /// reasoned about from the code alone: that punching connects, that a graceful leave actually
    /// reaches the other client, and that a client can rejoin afterwards.
    /// </summary>
    public class NatPunchSanityTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public NatPunchSanityTests(ITestOutputHelper output)
        {
            _output = output;

            // Eliminate thread-pool injection throttling so background pollers/continuations
            // aren't starved by blocking .Wait() calls (which otherwise throttle to ~1/sec).
            ThreadPool.SetMinThreads(64, 64);

            // Route client-side Serilog output (LiteNetP2PClient logs) into the test log,
            // and drop the default Seq sink so we don't depend on localhost:5341.
            Common.Logging.LogManager.Configuration = new Serilog.LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(new XunitSink(output));
        }

        [Fact]
        public void TwoClients_OnLoopback_EstablishP2PConnection()
        {
            // The helper itself asserts the pair connects; nothing more to check.
            WithTwoPunchedClients("sanity_instance", (clientA, watcherA, clientB, watcherB) => { });
        }

        [Fact]
        public void GracefulDisconnect_IsReceivedByOtherClient()
        {
            WithTwoPunchedClients("disconnect_instance", (clientA, watcherA, clientB, watcherB) =>
            {
                // A is connected to the rendezvous server AND to B, so record the count to prove the P2P
                // peer (B) specifically drops without depending on the absolute number.
                int peersBefore = clientA.ConnectedPeersCount;

                // Client B leaves the location exactly the way the live flow does on InstanceCleared.
                _output.WriteLine($"Client B leaving via DisconnectPeers()... (A had {peersBefore} peers)");
                clientB.DisconnectPeers();

                // THE question behind the leave/rejoin bug: does B's graceful disconnect actually reach
                // A as an OnPeerDisconnected event? If this fails, the staying client never learns the
                // leaver is gone — which is what we saw in the live logs.
                bool aSawDisconnect = WaitUntil(() => watcherA.Disconnected, 8000);
                _output.WriteLine($"A.Disconnected={watcherA.Disconnected}, A.peers={clientA.ConnectedPeersCount}");

                Assert.True(aSawDisconnect,
                    "Client A never received PeerDisconnected after Client B gracefully disconnected " +
                    "(DisconnectPeers). The staying client never learns the leaver is gone.");

                Assert.True(WaitUntil(() => clientA.ConnectedPeersCount < peersBefore, 3000),
                    $"Client A's connected-peer count did not drop after B left (still {clientA.ConnectedPeersCount}).");
            });
        }

        [Fact]
        public void ClientCanRejoin_AfterLeaving()
        {
            const string instance = "rejoin_instance";
            WithTwoPunchedClients(instance, (clientA, watcherA, clientB, watcherB) =>
            {
                // ---- B leaves ----
                _output.WriteLine("Client B leaving via DisconnectPeers()...");
                clientB.DisconnectPeers();
                Assert.True(WaitUntil(() => watcherA.Disconnected, 8000),
                    "Client A never saw B leave, so the rejoin scenario can't be evaluated.");

                // ---- B rejoins the same instance ----
                watcherA.Connected = false;
                watcherB.Connected = false;
                watcherA.Disconnected = false;

                _output.WriteLine("Client B rejoining (reconnect to rendezvous + re-punch)...");
                Assert.True(clientB.ConnectToP2PServer(), "Client B could not reconnect to the intro server on rejoin");
                Assert.True(WaitUntil(() => clientB.ConnectedPeersCount >= 1, 3000),
                    "Client B never re-registered with the intro server on rejoin");

                clientB.ConnectToInstance(instance);

                Assert.True(WaitUntil(() => watcherA.Connected && watcherB.Connected, 8000),
                    $"Clients did not re-establish a P2P link after B rejoined " +
                    $"(A.connected={watcherA.Connected}, B.connected={watcherB.Connected}).");
            });
        }

        /// <summary>
        /// Stands up the rendezvous server + two real clients, connects them to the server, punches both
        /// into <paramref name="instance"/>, waits until the direct P2P link is up, then runs
        /// <paramref name="body"/>. Tears everything (and the bound UDP port) down afterwards.
        /// </summary>
        private void WithTwoPunchedClients(string instance,
            Action<LiteNetP2PClient, ConnectionWatcher, LiteNetP2PClient, ConnectionWatcher> body)
        {
            var config = new NetworkConfiguration();
            _output.WriteLine($"Config: NATType={config.NATType}, LanAddr={config.LanAddress}:{config.LanPort}");

            // The intro server binds a fixed UDP port from config; serialize across any
            // concurrent/duplicate test executions so they don't collide on that port.
            using var portLock = new Mutex(false, "Global\\BannerlordCoop_NatPunchSanityTest");
            try { portLock.WaitOne(TimeSpan.FromSeconds(30)); } catch (AbandonedMutexException) { }

            var server = new MissionTestServer(config, new XunitMsLogger<MissionTestServer>(_output));
            _ = PumpServer(server);

            var watcherA = new ConnectionWatcher();
            var watcherB = new ConnectionWatcher();
            var clientA = NewClient(config, watcherA);
            var clientB = NewClient(config, watcherB);

            try
            {
                Assert.True(clientA.ConnectToP2PServer(), "Client A could not reach the intro server");
                Assert.True(clientB.ConnectToP2PServer(), "Client B could not reach the intro server");

                Assert.True(
                    WaitUntil(() => clientA.ConnectedPeersCount >= 1 && clientB.ConnectedPeersCount >= 1, 3000),
                    $"Clients never registered with the intro server (A={clientA.ConnectedPeersCount}, B={clientB.ConnectedPeersCount})");

                _output.WriteLine("Both clients connected to server; punching...");
                clientA.ConnectToInstance(instance);
                clientB.ConnectToInstance(instance);

                Assert.True(
                    WaitUntil(() => watcherA.Connected && watcherB.Connected, 8000),
                    $"NAT holepunch did not establish a P2P link. " +
                    $"A.peerConnected={watcherA.Connected}, B.peerConnected={watcherB.Connected}, " +
                    $"A.peers={clientA.ConnectedPeersCount}, B.peers={clientB.ConnectedPeersCount}");

                body(clientA, watcherA, clientB, watcherB);
            }
            finally
            {
                clientA.Dispose();
                clientB.Dispose();
                _cts.Cancel();
                StopServer(server); // release the bound UDP port for the next execution
                try { portLock.ReleaseMutex(); } catch { }
            }
        }

        // MissionTestServer owns a NetManager but exposes no Stop(); release its socket via reflection.
        private static void StopServer(MissionTestServer server)
        {
            try
            {
                var field = typeof(MissionTestServer).GetField("_netManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                (field?.GetValue(server) as LiteNetLib.NetManager)?.Stop();
            }
            catch { /* best effort */ }
        }

        private static LiteNetP2PClient NewClient(NetworkConfiguration config, ConnectionWatcher watcher)
        {
            var broker = new MessageBroker();
            // NOTE: MessageBroker stores subscribers as weak references to the delegate target,
            // so the watcher instance MUST stay alive for the duration of the test.
            broker.Subscribe<PeerConnected>(watcher.OnPeerConnected);
            broker.Subscribe<PeerDisconnected>(watcher.OnPeerDisconnected);
            // Each client needs a distinct, non-empty ControllerId (its P2P identity); otherwise the
            // intro server's registry conflates the two peers as one.
            var controllerIdProvider = new ControllerIdProvider();
            controllerIdProvider.SetControllerId(Guid.NewGuid().ToString());
            return new LiteNetP2PClient(config, new NoopSerializer(), broker, new PacketManager(), controllerIdProvider);
        }

        private Task PumpServer(MissionTestServer server) => Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                server.Update();
                await Task.Delay(8);
            }
        });

        private static bool WaitUntil(Func<bool> condition, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                Thread.Sleep(25);
            }
            return condition();
        }

        public void Dispose() => _cts.Cancel();

        // --- helpers ---

        private sealed class ConnectionWatcher
        {
            public volatile bool Connected;
            public volatile bool Disconnected;
            public void OnPeerConnected(MessagePayload<PeerConnected> _) => Connected = true;
            public void OnPeerDisconnected(MessagePayload<PeerDisconnected> _) => Disconnected = true;
        }

        private sealed class NoopSerializer : ICommonSerializer
        {
            public T Deserialize<T>(byte[] data) => default;
            public object Deserialize(byte[] data) => null;
            public byte[] Serialize(object obj) => Array.Empty<byte>();
        }

        private sealed class XunitSink : ILogEventSink
        {
            private readonly ITestOutputHelper _o;
            public XunitSink(ITestOutputHelper o) => _o = o;
            public void Emit(LogEvent e)
            {
                try { _o.WriteLine($"[CLI {e.Level,-11}] {e.RenderMessage()}"); }
                catch { /* test already finished */ }
            }
        }

        private sealed class XunitMsLogger<T> : ILogger<T>
        {
            private readonly ITestOutputHelper _o;
            public XunitMsLogger(ITestOutputHelper o) => _o = o;
            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
            public bool IsEnabled(MsLogLevel logLevel) => true;
            public void Log<TState>(MsLogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                try
                {
                    _o.WriteLine($"[SRV {logLevel,-11}] {formatter(state, exception)}");
                    if (exception != null) _o.WriteLine(exception.ToString());
                }
                catch { /* test already finished */ }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new NullScope();
                public void Dispose() { }
            }
        }
    }
}
