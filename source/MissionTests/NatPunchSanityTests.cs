using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
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
    /// End-to-end sanity check for NAT hole punching on loopback. Spins up the real
    /// <see cref="MissionTestServer"/> (rendezvous/intro server) and two real
    /// <see cref="LiteNetP2PClient"/> instances, then verifies that after both punch
    /// into the same instance they establish a direct peer-to-peer connection.
    ///
    /// This mirrors "two game instances on one machine" exactly (loopback UDP), with
    /// the only difference being all three live in one process.
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
            var config = new NetworkConfiguration();
            _output.WriteLine($"Config: NATType={config.NATType}, LanAddr={config.LanAddress}:{config.LanPort}, WanAddr={config.WanAddress}:{config.WanPort}");

            // The intro server binds a fixed UDP port from config; serialize across any
            // concurrent/duplicate test executions so they don't collide on that port.
            using var portLock = new Mutex(false, "Global\\BannerlordCoop_NatPunchSanityTest");
            try { portLock.WaitOne(TimeSpan.FromSeconds(30)); } catch (AbandonedMutexException) { }

            // ---- intro/rendezvous server ----
            var server = new MissionTestServer(config, new XunitMsLogger<MissionTestServer>(_output));
            _ = PumpServer(server);

            // ---- two peers ----
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

                const string instance = "sanity_instance";
                _output.WriteLine("Both clients connected to server; punching...");
                clientA.NatPunch(instance);
                clientB.NatPunch(instance);

                bool punched = WaitUntil(() => watcherA.Connected && watcherB.Connected, 8000);

                _output.WriteLine($"Result: A.peerConnected={watcherA.Connected} (peers={clientA.ConnectedPeersCount}), " +
                                  $"B.peerConnected={watcherB.Connected} (peers={clientB.ConnectedPeersCount})");

                Assert.True(punched,
                    $"NAT holepunch did not establish a P2P link. " +
                    $"A.peerConnected={watcherA.Connected}, B.peerConnected={watcherB.Connected}, " +
                    $"A.peers={clientA.ConnectedPeersCount}, B.peers={clientB.ConnectedPeersCount}");
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
            return new LiteNetP2PClient(config, new NoopSerializer(), broker, new PacketManager());
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
            public void OnPeerConnected(MessagePayload<PeerConnected> _) => Connected = true;
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
