using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Connection;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Client.Services.Connection;

public class ClientPacketGateTests
{
    private readonly TestMessageBroker messageBroker = new TestMessageBroker();
    private readonly PacketManager packetManager = new PacketManager();
    private readonly FakeSerializer serializer = new FakeSerializer();
    private readonly RecordingPacketHandler messagePacketHandler;
    private readonly ClientPacketGate gate;

    public ClientPacketGateTests()
    {
        messagePacketHandler = new RecordingPacketHandler(PacketType.Message);
        packetManager.RegisterPacketHandler(messagePacketHandler);

        gate = new ClientPacketGate(messageBroker, packetManager, serializer);
    }

    private MessagePacket CreateMessagePacket(Type messageType) =>
        new MessagePacket(serializer.RegisterPeekType(messageType));

    private MessagePacket CreateSaveDataPacket() =>
        CreateMessagePacket(typeof(NetworkGameSaveDataReceived));

    private MessagePacket CreateSyncPacket() =>
        CreateMessagePacket(typeof(TestSyncMessage));

    [Theory]
    [InlineData(typeof(NetworkClientValidated))]
    [InlineData(typeof(NetworkModuleVersionsValidated))]
    [InlineData(typeof(NetworkGameSaveDataReceived))]
    [InlineData(typeof(NetworkHeroRecieved))]
    [InlineData(typeof(NetworkNewPlayerHeroCreated))]
    [InlineData(typeof(NetworkTimeControlLockChanged))]
    [InlineData(typeof(NetworkMapEventLockChanged))]
    [InlineData(typeof(SendInformationMessage))]
    public void JoinMessages_AreNotHeld_WhileJoining(Type messageType)
    {
        var packet = CreateMessagePacket(messageType);

        Assert.False(gate.TryHold(null, packet));
    }

    [Fact]
    public void SyncPacket_BeforeSaveData_IsDiscarded()
    {
        var packet = CreateSyncPacket();

        Assert.True(gate.TryHold(null, packet));

        messageBroker.Publish(this, new ReleaseNetworkBacklog());

        Assert.Empty(messagePacketHandler.Received);
    }

    [Fact]
    public void SyncPackets_AfterSaveData_AreReplayedInOrder()
    {
        Assert.False(gate.TryHold(null, CreateSaveDataPacket()));

        var first = CreateSyncPacket();
        var second = CreateSyncPacket();
        Assert.True(gate.TryHold(null, first));
        Assert.True(gate.TryHold(null, second));
        Assert.Empty(messagePacketHandler.Received);

        messageBroker.Publish(this, new ReleaseNetworkBacklog());

        Assert.Equal(
            new IPacket[] { first, second },
            messagePacketHandler.Received);
    }

    [Fact]
    public void Replay_ContinuesPastFailingPacket()
    {
        Assert.False(gate.TryHold(null, CreateSaveDataPacket()));

        var poison = CreateSyncPacket();
        var healthy = CreateSyncPacket();
        packetManager.RegisterPacketHandler(new ThrowOnDataPacketHandler(poison.Data));

        Assert.True(gate.TryHold(null, poison));
        Assert.True(gate.TryHold(null, healthy));

        messageBroker.Publish(this, new ReleaseNetworkBacklog());

        // One failing packet must not abort the rest of the replay
        Assert.Equal(new IPacket[] { poison, healthy }, messagePacketHandler.Received);
    }

    [Fact]
    public void Gate_IsInactive_AfterBacklogRelease()
    {
        messageBroker.Publish(this, new ReleaseNetworkBacklog());

        Assert.False(gate.TryHold(null, CreateSyncPacket()));
    }

    [Fact]
    public void Disconnect_ClearsHeldPackets_AndReactivatesGate()
    {
        Assert.False(gate.TryHold(null, CreateSaveDataPacket()));
        Assert.True(gate.TryHold(null, CreateSyncPacket()));

        messageBroker.Publish(this, new NetworkDisconnected(default));

        // Rejoining starts a fresh snapshot cycle, so sync packets are discarded again
        Assert.True(gate.TryHold(null, CreateSyncPacket()));

        messageBroker.Publish(this, new ReleaseNetworkBacklog());

        Assert.Empty(messagePacketHandler.Received);
    }

    [Fact]
    public void NonMessagePacket_IsHeldAndReplayed_EvenBeforeSaveData()
    {
        // Unordered packets cannot be classified against the snapshot boundary,
        // so they are held from the start instead of discarded
        var behaviorPacketHandler = new RecordingPacketHandler(PacketType.UpdatePartyBehavior);
        packetManager.RegisterPacketHandler(behaviorPacketHandler);

        var packet = new BehaviorPacketStub();
        Assert.True(gate.TryHold(null, packet));

        messageBroker.Publish(this, new ReleaseNetworkBacklog());

        Assert.Equal(new IPacket[] { packet }, behaviorPacketHandler.Received);
    }

    [Fact]
    public void UnknownMessagePacket_IsNotHeld()
    {
        // Unknown data fails the type peek; the packet passes through so the
        // failure surfaces in the normal handling path
        var packet = new MessagePacket(new byte[] { 42 });

        Assert.False(gate.TryHold(null, packet));
    }

    [Fact]
    public void NullPacket_IsNotHeld()
    {
        Assert.False(gate.TryHold(null, null));
    }

    private record TestSyncMessage : IMessage;

    private class FakeSerializer : ICommonSerializer
    {
        // Keys are compared by reference: every registration returns a distinct
        // byte[] instance, and lookups only succeed for that same instance
        private readonly Dictionary<byte[], Type> peekTypes = new Dictionary<byte[], Type>();

        public byte[] RegisterPeekType(Type type)
        {
            var key = new byte[1];
            peekTypes.Add(key, type);
            return key;
        }

        public bool TryPeekType(byte[] data, out Type type) => peekTypes.TryGetValue(data, out type);

        public byte[] Serialize(object obj) => RegisterPeekType(obj.GetType());

        public T Deserialize<T>(byte[] data) => default;

        public object Deserialize(byte[] data) => null;
    }

    private class RecordingPacketHandler : IPacketHandler
    {
        public List<IPacket> Received { get; } = new List<IPacket>();

        public PacketType PacketType { get; }

        public RecordingPacketHandler(PacketType packetType)
        {
            PacketType = packetType;
        }

        public void HandlePacket(NetPeer peer, IPacket packet) => Received.Add(packet);

        public void Dispose()
        {
        }
    }

    private class ThrowOnDataPacketHandler : IPacketHandler
    {
        private readonly byte[] poisonData;

        public PacketType PacketType => PacketType.Message;

        public ThrowOnDataPacketHandler(byte[] poisonData)
        {
            this.poisonData = poisonData;
        }

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            if (packet is MessagePacket messagePacket && messagePacket.Data == poisonData)
                throw new InvalidOperationException("Poisoned packet");
        }

        public void Dispose()
        {
        }
    }

    private class BehaviorPacketStub : IPacket
    {
        public PacketType PacketType => PacketType.UpdatePartyBehavior;

        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableUnordered;
    }
}
