using Coop.Steam;
using Moq;
using Serilog;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Coop.Tests.Steam
{
    public class SteamNetworkingTunnelTransportBaseTests
    {
        [Fact]
        public void SendRateFloor_IsFourMiBPerSecond()
        {
            Assert.Equal(4 * 1024 * 1024, SteamTunnel.SendRateMinBytesPerSecond);
        }

        [Fact]
        public void Connections_UseSixtySecondConnectedTimeout()
        {
            const uint acceptedConnection = 42;
            const ulong remoteSteamId = 76561198000000042;
            using var transport = new TestSteamNetworkingTunnelTransport(Mock.Of<ILogger>());

            transport.EnsureRelayAccess();
            Assert.Contains(transport.ConfiguredValues, value =>
                value.Key == ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected &&
                value.Scope == ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global &&
                value.Value == 60_000);

            transport.ConnectToHost(remoteSteamId, SteamTunnel.VirtualPort);
            AssertConnectedTimeout(transport.ConnectOptions);

            transport.ListenForClients(SteamTunnel.VirtualPort);
            AssertConnectedTimeout(transport.ListenOptions);

            SeedOwnedConnection(transport, acceptedConnection, remoteSteamId);
            transport.AcceptConnection(acceptedConnection);
            Assert.Contains(transport.ConfiguredValues, value =>
                value.Key == ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected &&
                value.Scope == ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Connection &&
                value.ScopeObject == (IntPtr)acceptedConnection &&
                value.Value == 60_000);
        }

        [Fact]
        public void ClosedSubscriberThrows_StillClosesAndForgetsConnection()
        {
            const uint connection = 42;
            const ulong remoteSteamId = 76561198000000042;
            Exception? loggedException = null;
            var logger = new Mock<ILogger>();
            logger.Setup(value => value.Error(It.IsAny<Exception>(), It.IsAny<string>()))
                .Callback<Exception, string>((exception, _) => loggedException = exception);
            using var transport = new TestSteamNetworkingTunnelTransport(logger.Object);
            SeedOwnedConnection(transport, connection, remoteSteamId);
            Assert.True(transport.TryGetRemoteSteamId(connection, out var recordedSteamId));
            Assert.Equal(remoteSteamId, recordedSteamId);

            bool subscriberCalled = false;
            var subscriberFailure = new InvalidOperationException("scripted subscriber failure");
            transport.ConnectionStateChanged += (_, state) =>
            {
                if (state != TunnelConnectionState.Closed) return;

                subscriberCalled = true;
                throw subscriberFailure;
            };

            transport.DispatchConnectionState(
                connection,
                ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer);

            Assert.True(subscriberCalled, loggedException?.ToString() ?? "No callback error was logged");
            Assert.Same(subscriberFailure, loggedException);
            Assert.Equal(new[] { connection }, transport.RawClosedConnections);
            Assert.False(transport.TryGetRemoteSteamId(connection, out _));

            transport.CloseConnection(connection);
            Assert.Single(transport.RawClosedConnections);
        }

        // The stats line has to name the route, because ping alone cant tell a route switch apart.
        [Theory]
        [InlineData(Constants.k_nSteamNetworkConnectionInfoFlags_Relayed, "route=relayed")]
        [InlineData(Constants.k_nSteamNetworkConnectionInfoFlags_Relayed | Constants.k_nSteamNetworkConnectionInfoFlags_Fast, "route=relayed")]
        [InlineData(0, "route=direct")]
        [InlineData(Constants.k_nSteamNetworkConnectionInfoFlags_Fast, "route=direct")]
        public void DescribeConnection_NamesRouteFromConnectionFlags(int flags, string expectedRoute)
        {
            using var transport = new TestSteamNetworkingTunnelTransport(Mock.Of<ILogger>())
            {
                RealTimeStatusResult = EResult.k_EResultOK,
                ConnectionInfoFlags = flags,
            };

            Assert.EndsWith(expectedRoute, transport.DescribeConnection(42));
        }

        // A failed info read must not lose the rest of the stats line.
        [Fact]
        public void DescribeConnection_ReportsUnknownRouteWhenInfoIsUnavailable()
        {
            using var transport = new TestSteamNetworkingTunnelTransport(Mock.Of<ILogger>())
            {
                RealTimeStatusResult = EResult.k_EResultOK,
                ConnectionInfoFlags = null,
            };

            string description = transport.DescribeConnection(42);

            Assert.StartsWith("sendRate=", description);
            Assert.EndsWith("route=unknown", description);
        }

        // A join that fails before finding a route has no Relayed flag, which must not read as direct.
        [Theory]
        [InlineData(ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)]
        [InlineData(ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute)]
        [InlineData(ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer)]
        [InlineData(ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)]
        public void DescribeConnection_ReportsUnknownRouteWhenNotConnected(ESteamNetworkingConnectionState state)
        {
            using var transport = new TestSteamNetworkingTunnelTransport(Mock.Of<ILogger>())
            {
                RealTimeStatusResult = EResult.k_EResultOK,
                ConnectionInfoFlags = 0,
                ConnectionInfoState = state,
            };

            Assert.EndsWith("route=unknown", transport.DescribeConnection(42));
        }

        private static void SeedOwnedConnection(
            SteamNetworkingTunnelTransportBase transport,
            uint connection,
            ulong remoteSteamId)
        {
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            var transportType = typeof(SteamNetworkingTunnelTransportBase);
            var ownedConnections = Assert.IsType<HashSet<uint>>(
                transportType.GetField("ownedConnections", privateInstance)?.GetValue(transport));
            var remoteIdentities = Assert.IsType<TunnelConnectionIdentityRegistry>(
                transportType.GetField("remoteIdentities", privateInstance)?.GetValue(transport));

            ownedConnections.Add(connection);
            remoteIdentities.Record(connection, remoteSteamId);
        }

        private static void AssertConnectedTimeout(SteamNetworkingConfigValue_t[] options)
        {
            var timeout = Assert.Single(options, option =>
                option.m_eValue == ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected);
            Assert.Equal(60_000, timeout.m_val.m_int32);
        }

        private sealed class TestSteamNetworkingTunnelTransport : SteamNetworkingTunnelTransportBase
        {
            private Callback<SteamNetConnectionStatusChangedCallback_t>.DispatchDelegate? statusChangedHandler;

            public TestSteamNetworkingTunnelTransport(ILogger logger)
                : base(logger)
            {
            }

            public EResult RealTimeStatusResult { get; set; } = EResult.k_EResultFail;
            public int? ConnectionInfoFlags { get; set; }
            public ESteamNetworkingConnectionState ConnectionInfoState { get; set; } =
                ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected;
            public List<uint> RawClosedConnections { get; } = new List<uint>();
            public List<(ESteamNetworkingConfigValue Key, ESteamNetworkingConfigScope Scope, IntPtr ScopeObject, int Value)>
                ConfiguredValues { get; } =
                    new List<(ESteamNetworkingConfigValue, ESteamNetworkingConfigScope, IntPtr, int)>();
            public SteamNetworkingConfigValue_t[] ConnectOptions { get; private set; } =
                Array.Empty<SteamNetworkingConfigValue_t>();
            public SteamNetworkingConfigValue_t[] ListenOptions { get; private set; } =
                Array.Empty<SteamNetworkingConfigValue_t>();

            public void DispatchConnectionState(uint connection, ESteamNetworkingConnectionState state)
            {
                statusChangedHandler!(new SteamNetConnectionStatusChangedCallback_t
                {
                    m_hConn = new HSteamNetConnection(connection),
                    m_info = CreateConnectionInfo(state),
                });
            }

            private static SteamNetConnectionInfo_t CreateConnectionInfo(
                ESteamNetworkingConnectionState state)
            {
                object boxedInfo = new SteamNetConnectionInfo_t { m_eState = state };
                var endDebugField = typeof(SteamNetConnectionInfo_t).GetField(
                    "m_szEndDebug_",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(endDebugField);
                endDebugField.SetValue(boxedInfo, new byte[128]);
                return (SteamNetConnectionInfo_t)boxedInfo;
            }

            protected override Callback<SteamNetConnectionStatusChangedCallback_t> CreateStatusChangedCallback(
                Callback<SteamNetConnectionStatusChangedCallback_t>.DispatchDelegate handler)
            {
                statusChangedHandler = handler;
                return null!;
            }

            protected override void InitRelayNetworkAccess()
            {
            }

            protected override bool SetConfigValue(
                ESteamNetworkingConfigValue key,
                ESteamNetworkingConfigScope scope,
                IntPtr scopeObj,
                ESteamNetworkingConfigDataType type,
                IntPtr arg)
            {
                ConfiguredValues.Add((key, scope, scopeObj, Marshal.ReadInt32(arg)));
                return true;
            }

            protected override ESteamNetworkingGetConfigValueResult GetConfigValue(
                ESteamNetworkingConfigValue key,
                ESteamNetworkingConfigScope scope,
                IntPtr scopeObj,
                out ESteamNetworkingConfigDataType type,
                IntPtr result,
                ref ulong size)
            {
                type = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32;
                return ESteamNetworkingGetConfigValueResult.k_ESteamNetworkingGetConfigValue_BadValue;
            }

            protected override EResult GetConnectionRealTimeStatus(
                HSteamNetConnection conn,
                ref SteamNetConnectionRealTimeStatus_t status,
                int lanes,
                ref SteamNetConnectionRealTimeLaneStatus_t laneStatus) => RealTimeStatusResult;

            // Scripts the route flags, where null stands for Steam refusing the read.
            protected override bool GetConnectionInfo(HSteamNetConnection conn, out SteamNetConnectionInfo_t info)
            {
                info = new SteamNetConnectionInfo_t { m_nFlags = ConnectionInfoFlags ?? 0, m_eState = ConnectionInfoState };
                return ConnectionInfoFlags.HasValue;
            }

            protected override HSteamNetConnection ConnectP2P(
                ref SteamNetworkingIdentity identity,
                int virtualPort,
                int options,
                SteamNetworkingConfigValue_t[] optionValues)
            {
                ConnectOptions = optionValues;
                return new HSteamNetConnection { m_HSteamNetConnection = 44 };
            }

            protected override SteamNetworkingIdentity CreateSteamIdentity(ulong steamId) => default;

            protected override HSteamListenSocket CreateListenSocketP2P(
                int virtualPort,
                int options,
                SteamNetworkingConfigValue_t[] optionValues)
            {
                ListenOptions = optionValues;
                return new HSteamListenSocket { m_HSteamListenSocket = 43 };
            }

            protected override bool CloseListenSocket(HSteamListenSocket socket) => true;

            protected override EResult AcceptConnectionRaw(HSteamNetConnection conn) => EResult.k_EResultOK;

            protected override bool CloseConnectionRaw(
                HSteamNetConnection conn,
                int reason,
                string debug,
                bool enableLinger)
            {
                RawClosedConnections.Add(conn.m_HSteamNetConnection);
                return true;
            }

            protected override EResult SendMessageToConnection(
                HSteamNetConnection conn,
                IntPtr data,
                uint size,
                int flags,
                out long messageNumber)
            {
                messageNumber = 0;
                return EResult.k_EResultOK;
            }

            protected override int ReceiveMessagesOnConnection(
                HSteamNetConnection conn,
                IntPtr[] messages,
                int maxMessages) => 0;
        }
    }
}
