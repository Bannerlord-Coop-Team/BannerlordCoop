using Common.Messaging;
using Common.Logging;
using Common.Util;
using System.Net.Sockets;
using Common.Network;
using Common.Network.Data;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using E2E.Tests.Environment.Extensions;
using GameInterface.Services.Entity;
using HarmonyLib;
using LiteNetLib;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System.Collections.Concurrent;
using System.Reflection;
using System.Net;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

public class LiteNetP2PClientTests
{
    private static NatPunchModule? capturedNatModule;
    private static readonly List<string> CapturedPunchTokens = new();

    [Fact]
    public void IntroductionAuthorizationIgnoresStaleReplyAfterSameInstanceReentry()
    {
        var config = new Mock<INetworkConfig>();
        var relayNetwork = new Mock<IRelayNetwork>();
        relayNetwork.SetupGet(network => network.ServerEndpoint)
            .Returns(new IPEndPoint(IPAddress.Loopback, 53001));
        var requests = new List<NetworkRequestMissionIntroduction>();
        relayNetwork.Setup(network => network.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => requests.Add(Assert.IsType<NetworkRequestMissionIntroduction>(message)));
        var broker = new TestMessageBroker();
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        using var client = new LiteNetP2PClient(
            config.Object,
            relayNetwork.Object,
            Mock.Of<IMissionContext>(),
            serializer,
            broker,
            Mock.Of<IPacketManager>(),
            Mock.Of<IMessagePacketHandler>(),
            Mock.Of<IControllerIdProvider>(),
            Mock.Of<ISteamMissionBridge>(),
            new MovementPacketCompressor(serializer),
            Mock.Of<IReliableMessageBatcher<string>>(),
            () => new ReceivePathDiagnostics());
        var requestField = typeof(LiteNetP2PClient).GetField(
            "introductionRequestId", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var netManager = (NetManager)typeof(LiteNetP2PClient)
            .GetField("netManager", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;
        var sendRequest = AccessTools.Method(typeof(NatPunchModule), nameof(NatPunchModule.SendNatIntroduceRequest),
            new[] { typeof(IPEndPoint), typeof(string) });
        var harmony = new Harmony($"coop.tests.mission-authorization.{Guid.NewGuid():N}");
        harmony.Patch(sendRequest, prefix: new HarmonyMethod(typeof(LiteNetP2PClientTests), nameof(CapturePunch)));
        capturedNatModule = netManager.NatPunchModule;
        CapturedPunchTokens.Clear();
        try
        {
            client.ConnectToInstance("town_ES1|tavern");
            var firstRequest = Assert.Single(requests);
            client.DisconnectPeers();
            broker.Publish(this, new NetworkMissionIntroductionAuthorized(
                firstRequest.InstanceId, firstRequest.RequestId, "unused"));
            Assert.Equal(Guid.Empty, requestField.GetValue(client));

            client.ConnectToInstance(firstRequest.InstanceId);
            Assert.Equal(2, requests.Count);
            var currentRequest = requests[1];
            Assert.NotEqual(firstRequest.RequestId, currentRequest.RequestId);
            broker.Publish(this, new NetworkMissionIntroductionAuthorized(
                firstRequest.InstanceId, firstRequest.RequestId, "unused"));
            Assert.Equal(currentRequest.RequestId, requestField.GetValue(client));
            Assert.Empty(CapturedPunchTokens);

            string token = Guid.NewGuid().ToString("N");
            var reply = new NetworkMissionIntroductionAuthorized(currentRequest.InstanceId, currentRequest.RequestId, token);
            broker.Publish(this, reply);
            Assert.Equal(Guid.Empty, requestField.GetValue(client));
            broker.Publish(this, reply);
            Assert.Equal(Guid.Empty, requestField.GetValue(client));
            Assert.Equal(token, Assert.Single(CapturedPunchTokens));
        }
        finally
        {
            capturedNatModule = null;
            CapturedPunchTokens.Clear();
            harmony.Unpatch(sendRequest, HarmonyPatchType.Prefix, harmony.Id);
        }
    }

    private static bool CapturePunch(NatPunchModule __instance, string additionalInfo)
    {
        if (!ReferenceEquals(__instance, capturedNatModule)) return true;
        CapturedPunchTokens.Add(additionalInfo);
        return false;
    }

    [Fact]
    public async Task PeerPromotion_ConcurrentReliableSend_DoesNotInvertLocks()
    {
        const string controllerId = "promoting-peer";
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var config = new Mock<INetworkConfig>();
        var relayNetwork = new Mock<IRelayNetwork>();
        var missionContext = new Mock<IMissionContext>();
        var routeOrder = new ConcurrentQueue<string>();
        relayNetwork
            .Setup(network => network.SendAll(It.IsAny<IPacket>()))
            .Callback<IPacket>(_ => routeOrder.Enqueue("relay"));
        missionContext
            .Setup(context => context.MapPeer(controllerId, It.IsAny<NetPeer>()))
            .Callback<string, NetPeer>((_, _) => routeOrder.Enqueue("map"));
        var messageBroker = new Mock<IMessageBroker>();
        var packetManager = new Mock<IPacketManager>();
        var messagePacketHandler = new Mock<IMessagePacketHandler>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        var steamBridge = new Mock<ISteamMissionBridge>();
        using var batcher = new PromotionRaceBatcher();
        using var client = new LiteNetP2PClient(
            config.Object,
            relayNetwork.Object,
            missionContext.Object,
            serializer,
            messageBroker.Object,
            packetManager.Object,
            messagePacketHandler.Object,
            controllerIdProvider.Object,
            steamBridge.Object,
            new MovementPacketCompressor(serializer),
            batcher, () => new Common.Logging.ReceivePathDiagnostics());
        NetPeer peer = NetPeerExtensions.CreatePeer(97);
        GetPendingPeerControllers(client)[peer] = controllerId;
        Guid peerCredential = Guid.NewGuid();
        GetControllerPeerCredentials(client)[controllerId] = peerCredential;
        GetPeerCredentials(client)[peer] = peerCredential;

        Task reliableSend = Task.Run(() => client.Send(
            controllerId,
            new NetworkBattleAgentDied(
                Guid.NewGuid(),
                wounded: false,
                Guid.Empty,
                inflictedDamage: 1,
                BoneBodyPartType.Head,
                deathAction: 0)));
        Assert.True(batcher.SendEntered.Wait(TimeSpan.FromSeconds(5)));

        Task promotion = Task.Run(() => client.OnPeerConnected(peer));

        Task concurrentWork = Task.WhenAll(reliableSend, promotion);
        Task completed = await Task.WhenAny(
            concurrentWork,
            Task.Delay(TimeSpan.FromSeconds(15)));

        Assert.Same(concurrentWork, completed);
        await concurrentWork;
        Assert.True(batcher.PromotionFlushAcquiredBuffer);
        Assert.Equal(new[] { "relay", "map" }, routeOrder);
        missionContext.Verify(context => context.MapPeer(controllerId, peer), Times.Once);
    }

    [Theory]
    [InlineData(false, DeliveryMethod.ReliableOrdered)]
    [InlineData(true, DeliveryMethod.ReliableOrdered)]
    [InlineData(false, DeliveryMethod.Unreliable)]
    [InlineData(true, DeliveryMethod.Unreliable)]
    public void OnNetworkReceive_CountsMappingDecisionWithoutChangingDispatch(bool mapped, DeliveryMethod deliveryMethod)
    {
        var serializer = new Mock<ICommonSerializer>();
        var packet = new Mock<IPacket>();
        byte[] bytes = { 1, 2, 3 };
        serializer.Setup(s => s.Deserialize(It.IsAny<byte[]>())).Returns(packet.Object);
        var packetManager = new Mock<IPacketManager>();
        var context = new Mock<IMissionContext>();
        var diagnostics = new Mock<IReceivePathDiagnostics>();
        using var client = new LiteNetP2PClient(
            Mock.Of<INetworkConfig>(), Mock.Of<IRelayNetwork>(), context.Object,
            serializer.Object, Mock.Of<IMessageBroker>(), packetManager.Object,
            Mock.Of<IMessagePacketHandler>(), Mock.Of<IControllerIdProvider>(),
            Mock.Of<ISteamMissionBridge>(), Mock.Of<IMovementPacketCompressor>(),
            new ReliableMessageBatcher<string>(serializer.Object), () => diagnostics.Object);
        client.ConnectToInstance("diagnostic-instance");
        NetPeer peer = NetPeerExtensions.CreatePeer(98);
        GetPendingPeerControllers(client)[peer] = "receiving-peer";
        if (mapped)
        {
            Guid credential = Guid.NewGuid();
            GetControllerPeerCredentials(client)["receiving-peer"] = credential;
            GetPeerCredentials(client)[peer] = credential;
            client.OnPeerConnected(peer);
        }
        var reader = ObjectHelper.SkipConstructor<NetPacketReader>();
        reader.SetSource(bytes);

        client.OnNetworkReceive(peer, reader, 0, deliveryMethod);

        bool buffered = !mapped && deliveryMethod == DeliveryMethod.ReliableOrdered;
        ReceivePathEvent expectedEvent = mapped ? ReceivePathEvent.MappedReceive :
            buffered ? ReceivePathEvent.PendingReliableBuffered : ReceivePathEvent.UnmappedDrop;
        diagnostics.Verify(d => d.Record(
            expectedEvent, 3, SocketError.Success), Times.Once);
        serializer.Verify(s => s.Deserialize(It.IsAny<byte[]>()), mapped ? Times.Once() : Times.Never());
        packetManager.Verify(p => p.HandleReceive(peer, packet.Object), mapped ? Times.Once() : Times.Never());
        Assert.Equal(mapped || buffered ? 0 : 3, reader.AvailableBytes);
    }

    private static Dictionary<NetPeer, string> GetPendingPeerControllers(LiteNetP2PClient client)
    {
        FieldInfo field = typeof(LiteNetP2PClient).GetField(
            "pendingPeerControllers",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<Dictionary<NetPeer, string>>(field.GetValue(client));
    }

    private static Dictionary<string, Guid> GetControllerPeerCredentials(LiteNetP2PClient client)
    {
        FieldInfo field = typeof(LiteNetP2PClient).GetField(
            "controllerPeerCredentials",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<Dictionary<string, Guid>>(field.GetValue(client));
    }

    private static Dictionary<NetPeer, Guid> GetPeerCredentials(LiteNetP2PClient client)
    {
        FieldInfo field = typeof(LiteNetP2PClient).GetField(
            "peerCredentials",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<Dictionary<NetPeer, Guid>>(field.GetValue(client));
    }

    private sealed class PromotionRaceBatcher : IReliableMessageBatcher<string>, IDisposable
    {
        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);
        private readonly object destinationBuffer = new object();
        private readonly ManualResetEventSlim promotionFlushEntered = new ManualResetEventSlim(false);

        public int BudgetBytes => ReliableMessageBatcher<string>.DefaultBudgetBytes;
        public ManualResetEventSlim SendEntered { get; } = new ManualResetEventSlim(false);
        public bool PromotionFlushAcquiredBuffer { get; private set; }

        public event Action<AggregateMessagePacket, int> AggregateSent
        {
            add { }
            remove { }
        }

        public void Send(
            string destination,
            byte[] messagePayload,
            Action<string, byte[]> sendReliableOrdered)
        {
            lock (destinationBuffer)
            {
                SendEntered.Set();
                if (!promotionFlushEntered.Wait(LockTimeout))
                    throw new TimeoutException("Peer promotion did not start its reliable flush");

                sendReliableOrdered(destination, messagePayload);
            }
        }

        public void SendImmediate(
            string destination,
            byte[] messagePayload,
            Action<string, byte[]> sendReliableOrdered)
        {
            Send(destination, messagePayload, sendReliableOrdered);
        }

        public void Flush(
            string destination,
            Action<string, byte[]> sendReliableOrdered)
        {
            EnterPromotionFlush(null);
        }

        public void FlushThen(
            string destination,
            Action<string, byte[]> sendReliableOrdered,
            Action sendAfterFlush)
        {
            EnterPromotionFlush(sendAfterFlush);
        }

        private void EnterPromotionFlush(Action? sendAfterFlush)
        {
            promotionFlushEntered.Set();
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(destinationBuffer, LockTimeout, ref lockTaken);
                PromotionFlushAcquiredBuffer = lockTaken;
                if (lockTaken) sendAfterFlush?.Invoke();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(destinationBuffer);
            }
        }

        public void FlushAll(
            Func<string, bool> isConnected,
            Action<string, byte[]> sendReliableOrdered)
        {
        }

        public void Remove(string destination)
        {
        }

        public void Clear()
        {
        }

        public void Dispose()
        {
            promotionFlushEntered.Dispose();
            SendEntered.Dispose();
        }
    }
}
