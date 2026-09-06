using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using E2E.Tests.Environment.Extensions;
using LiteNetLib;

namespace E2E.Tests.Environment.Mock;

public abstract class MockNetworkBase : INetwork
{
    private readonly TestNetworkRouter networkOrchestrator;
    private readonly IPacketManager packetManager;
    private readonly ICommonSerializer serializer;
    private readonly IReliableMessageBatcher<NetPeer> reliableMessageBatcher;
    public static int InstanceCount = 0;

    public MockNetworkBase(
        TestNetworkRouter networkOrchestrator,
        IPacketManager packetManager,
        ICommonSerializer serializer,
        IReliableMessageBatcher<NetPeer> reliableMessageBatcher)
    {
        this.networkOrchestrator = networkOrchestrator;
        this.packetManager = packetManager;
        this.serializer = serializer;
        this.reliableMessageBatcher = reliableMessageBatcher;
        this.reliableMessageBatcher.AggregateSent += OnAggregateSent;
        this.networkOrchestrator.PeerConnectionGenerationChanged += OnPeerConnectionGenerationChanged;
        InstanceCount = Interlocked.Increment(ref InstanceCount);

        NetPeer = NetPeerExtensions.CreatePeer(InstanceCount);
        this.networkOrchestrator.AddNetwork(this);
    }

    public INetworkConfig Config => throw new NotImplementedException();

    public int Priority => throw new NotImplementedException();

    public NetPeer NetPeer { get; } = NetPeerExtensions.CreatePeer();

    public MessageCollection NetworkSentMessages { get; } = new MessageCollection();
    public MessageCollection NetworkSentImmediateMessages { get; } = new MessageCollection();
    public PacketCollection NetworkSentPackets { get; } = new PacketCollection();

    public void ReceiveFromNetwork(NetPeer peer, IPacket packet) => packetManager.HandleReceive(peer, packet);

    public void Send(NetPeer netPeer, IPacket packet)
    {
        NetworkSentPackets.Add(packet);
        SendPacket(netPeer, packet);
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        NetworkSentMessages.Add(message);
        SendMessage(netPeer, message, immediate: false);
    }

    public void SendImmediate(NetPeer netPeer, IPacket packet)
    {
        NetworkSentPackets.Add(packet);
        if (packet is MessagePacket messagePacket)
        {
            // Production only treats the IMessage overload as a reliable-ordered barrier. A caller
            // passing an already-framed MessagePacket keeps the normal aggregation behavior.
            reliableMessageBatcher.Send(netPeer, messagePacket.Data, SendReliablePayload);
            return;
        }

        SendPacket(netPeer, packet);
    }

    public void SendImmediate(NetPeer netPeer, IMessage message)
    {
        NetworkSentImmediateMessages.Add(message);
        NetworkSentMessages.Add(message);
        SendMessage(netPeer, message, immediate: true);
    }

    public void SendAll(IPacket packet)
    {
        NetworkSentPackets.Add(packet);
        foreach (NetPeer recipient in networkOrchestrator.GetRecipients(NetPeer))
        {
            SendPacket(recipient, packet);
        }
    }

    public void SendAll(IMessage message)
    {
        NetworkSentMessages.Add(message);
        foreach (NetPeer recipient in networkOrchestrator.GetRecipients(NetPeer))
        {
            SendMessage(recipient, message, immediate: false);
        }
    }

    public void SendAllBut(NetPeer excludedPeer, IPacket packet)
    {
        NetworkSentPackets.Add(packet);
        foreach (NetPeer recipient in networkOrchestrator.GetRecipients(NetPeer, excludedPeer))
        {
            SendPacket(recipient, packet);
        }
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        NetworkSentMessages.Add(message);
        foreach (NetPeer recipient in networkOrchestrator.GetRecipients(NetPeer, excludedPeer))
        {
            SendMessage(recipient, message, immediate: false);
        }
    }

    public void FlushPendingMessages()
    {
        FlushPendingMessageBatch();
    }

    public void Start()
    {
        throw new NotImplementedException();
    }

    public void Stop()
    {
        throw new NotImplementedException();
    }

    public void Update(TimeSpan frameTime)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        networkOrchestrator.RemoveNetwork(this);
        networkOrchestrator.PeerConnectionGenerationChanged -= OnPeerConnectionGenerationChanged;
        reliableMessageBatcher.AggregateSent -= OnAggregateSent;
        reliableMessageBatcher.Clear();
    }

    private void SendMessage(NetPeer recipient, IMessage message, bool immediate)
    {
        MessagePacket packet = MessagePacket.Create(message, serializer);
        if (immediate)
        {
            reliableMessageBatcher.SendImmediate(recipient, packet.Data, SendReliablePayload);
            return;
        }

        reliableMessageBatcher.Send(recipient, packet.Data, SendReliablePayload);
    }

    private void SendPacket(NetPeer recipient, IPacket packet)
    {
        if (packet is MessagePacket messagePacket)
        {
            reliableMessageBatcher.Send(recipient, messagePacket.Data, SendReliablePayload);
            return;
        }

        if (packet.DeliveryMethod == DeliveryMethod.ReliableOrdered ||
            packet.DeliveryMethod == DeliveryMethod.ReliableUnordered)
        {
            FlushThenSendPacket(recipient, packet);
            return;
        }

        networkOrchestrator.Send(NetPeer, recipient, packet);
    }

    private void FlushThenSendPacket(NetPeer recipient, IPacket packet)
    {
        reliableMessageBatcher.FlushThen(
            recipient,
            SendReliablePayload,
            () => networkOrchestrator.Send(NetPeer, recipient, packet));
    }

    private void SendReliablePayload(NetPeer recipient, byte[] payload)
    {
        networkOrchestrator.SendReliablePayload(NetPeer, recipient, payload);
    }

    internal int FlushPendingMessageBatch()
    {
        int sentPayloads = 0;
        reliableMessageBatcher.FlushAll(
            networkOrchestrator.IsConnected,
            (recipient, payload) =>
            {
                sentPayloads++;
                SendReliablePayload(recipient, payload);
            });
        return sentPayloads;
    }

    internal void FlushNetworkTick()
    {
        networkOrchestrator.FlushNetworkTick();
    }

    private void OnAggregateSent(AggregateMessagePacket packet, int framingOverhead)
    {
        NetworkSentPackets.Add(packet);
    }

    private void OnPeerConnectionGenerationChanged(NetPeer changedPeer)
    {
        if (changedPeer == NetPeer)
        {
            reliableMessageBatcher.Clear();
            return;
        }

        reliableMessageBatcher.Remove(changedPeer);
    }
}
