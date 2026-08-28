using Common.Messaging;
using Common.Network;
using Common.Network.Data;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Extensions;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System.Collections.Concurrent;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

public class LiteNetP2PClientTests
{
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
            batcher);
        NetPeer peer = NetPeerExtensions.CreatePeer(97);
        GetPendingPeerControllers(client)[peer] = controllerId;

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

    private static Dictionary<NetPeer, string> GetPendingPeerControllers(LiteNetP2PClient client)
    {
        FieldInfo field = typeof(LiteNetP2PClient).GetField(
            "pendingPeerControllers",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<Dictionary<NetPeer, string>>(field.GetValue(client));
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
