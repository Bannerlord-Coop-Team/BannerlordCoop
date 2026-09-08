using Autofac;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using Coop.Core.Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using LiteNetLib;

namespace E2E.Tests.Environment;

public enum TestNetworkReceiveContext
{
    GameThread,
    PollerThread,
}

/// <summary>
/// Network message router for simulating messages across the network
/// </summary>
public class TestNetworkRouter
{
    private const byte WorldChannel = 0;

    private readonly IVirtualNetworkScheduler scheduler;
    private ServerInstance Server;
    private readonly List<ClientInstance> Clients = new List<ClientInstance>();
    private readonly List<MockNetworkBase> networks = new List<MockNetworkBase>();
    private int networkTickActive;

    internal event Action<NetPeer>? PeerConnectionGenerationChanged;

    public TestNetworkRouter() : this(new VirtualNetworkScheduler())
    {
    }

    public TestNetworkRouter(IVirtualNetworkScheduler scheduler)
    {
        if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
        this.scheduler = scheduler;
    }

    /// <summary>
    /// Controls whether receive handlers run with the compatibility game-thread mark or the real poller-thread
    /// identity. Poller-thread delivery leaves marshalled work queued until the recipient is pumped explicitly.
    /// </summary>
    public TestNetworkReceiveContext ReceiveContext { get; set; } = TestNetworkReceiveContext.GameThread;

    /// <summary>
    /// Drains traffic due at the scheduler's current time after each send. The default preserves the historical
    /// synchronous harness behavior; turn it off when a test needs to control delivery explicitly.
    /// </summary>
    public bool AutoDrainReady { get; set; } = true;

    public TimeSpan CurrentTime => scheduler.CurrentTime;

    public TimeSpan DefaultLatency
    {
        get => scheduler.DefaultLatency;
        set => scheduler.DefaultLatency = value;
    }

    public int PendingDeliveryCount => scheduler.PendingDeliveryCount;
    public IReadOnlyList<VirtualNetworkTraceEntry> Trace => scheduler.Trace;

    public void AddServer(ServerInstance instance)
    {
        Server = instance;
    }

    public void AddClient(ClientInstance instance)
    {
        Clients.Add(instance);
    }

    internal void AddNetwork(MockNetworkBase network)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));
        lock (networks)
        {
            networks.Add(network);
        }
    }

    internal void RemoveNetwork(MockNetworkBase network)
    {
        if (network == null) return;
        lock (networks)
        {
            networks.Remove(network);
        }
    }

    internal void FlushNetworkTick(int maximumPasses = 100)
    {
        if (maximumPasses <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumPasses));
        if (Interlocked.CompareExchange(ref networkTickActive, 1, 0) != 0)
            return;

        try
        {
            for (int pass = 0; pass < maximumPasses; pass++)
            {
                MockNetworkBase[] snapshot;
                lock (networks)
                {
                    snapshot = networks.ToArray();
                }

                int sentPayloads = 0;
                foreach (MockNetworkBase network in snapshot)
                {
                    sentPayloads += network.FlushPendingMessageBatch();
                }

                if (sentPayloads == 0)
                    return;
            }

            throw new InvalidOperationException(
                $"Reliable message traffic did not settle within {maximumPasses} simulated network pass(es).");
        }
        finally
        {
            Interlocked.Exchange(ref networkTickActive, 0);
        }
    }

    public IReadOnlyList<NetPeer> GetRecipients(NetPeer sender, NetPeer? ignored = null)
    {
        ValidatePeer(sender, nameof(sender));
        if (ignored != null) ValidatePeer(ignored, nameof(ignored));

        if (sender == Server.NetPeer)
        {
            return Clients
                .Select(client => client.NetPeer)
                .Where(peer => peer != ignored && scheduler.IsConnected(peer))
                .ToArray();
        }

        return ignored == Server.NetPeer || !scheduler.IsConnected(Server.NetPeer)
            ? Array.Empty<NetPeer>()
            : new[] { Server.NetPeer };
    }

    public void SetLatency(NetPeer sender, NetPeer receiver, TimeSpan latency)
    {
        ValidatePeer(sender, nameof(sender));
        ValidatePeer(receiver, nameof(receiver));
        scheduler.SetLatency(sender, receiver, latency);
    }

    public void ClearLatency(NetPeer sender, NetPeer receiver)
    {
        ValidatePeer(sender, nameof(sender));
        ValidatePeer(receiver, nameof(receiver));
        scheduler.ClearLatency(sender, receiver);
    }

    public void PauseLink(NetPeer sender, NetPeer receiver)
    {
        ValidatePeer(sender, nameof(sender));
        ValidatePeer(receiver, nameof(receiver));
        scheduler.PauseLink(sender, receiver);
    }

    public void ResumeLink(NetPeer sender, NetPeer receiver)
    {
        ValidatePeer(sender, nameof(sender));
        ValidatePeer(receiver, nameof(receiver));
        scheduler.ResumeLink(sender, receiver);
    }

    public bool IsLinkPaused(NetPeer sender, NetPeer receiver)
    {
        ValidatePeer(sender, nameof(sender));
        ValidatePeer(receiver, nameof(receiver));
        return scheduler.IsLinkPaused(sender, receiver);
    }

    public long GetConnectionGeneration(NetPeer peer)
    {
        ValidatePeer(peer, nameof(peer));
        return scheduler.GetConnectionGeneration(peer);
    }

    public bool IsConnected(NetPeer peer)
    {
        ValidatePeer(peer, nameof(peer));
        return scheduler.IsConnected(peer);
    }

    public int Disconnect(NetPeer peer)
    {
        ValidatePeer(peer, nameof(peer));
        int canceled = scheduler.Disconnect(peer);
        PeerConnectionGenerationChanged?.Invoke(peer);
        return canceled;
    }

    public void Reconnect(NetPeer peer)
    {
        ValidatePeer(peer, nameof(peer));
        scheduler.Reconnect(peer);
        PeerConnectionGenerationChanged?.Invoke(peer);
    }

    public int AdvanceBy(TimeSpan elapsed) => scheduler.AdvanceBy(elapsed);
    public int DrainReady() => scheduler.DrainReady();
    public int RunUntilIdle() => scheduler.RunUntilIdle();
    public void ClearTrace() => scheduler.ClearTrace();

    public void Send(NetPeer sender, NetPeer receiver, IMessage message)
    {
        byte[] serializedMessage = SenderInstance(sender).SerializeForWire(message);

        if (receiver == Server.NetPeer)
        {
            DeliverMessage(Server, sender, serializedMessage);
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            DeliverMessage(receivingClient, sender, serializedMessage);
        }
    }

    public void SendReliablePayload(NetPeer sender, NetPeer receiver, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidatePeer(sender, nameof(sender));
        ValidatePeer(receiver, nameof(receiver));

        EnvironmentInstance recipient = receiver == Server.NetPeer
            ? Server
            : Clients.Single(client => client.NetPeer == receiver);
        DeliverPayload(
            recipient,
            sender,
            payload,
            DeliveryMethod.ReliableOrdered,
            WorldChannel);
    }
    public void SendAll(NetPeer sender, IMessage message)
    {
        byte[] serializedMessage = SenderInstance(sender).SerializeForWire(message);

        foreach (NetPeer recipient in GetRecipients(sender))
        {
            DeliverMessage(RecipientInstance(recipient), sender, serializedMessage);
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IMessage message)
    {
        byte[] serializedMessage = SenderInstance(sender).SerializeForWire(message);

        foreach (NetPeer recipient in GetRecipients(sender, ignored))
        {
            DeliverMessage(RecipientInstance(recipient), sender, serializedMessage);
        }
    }

    public void Send(NetPeer sender, NetPeer receiver, IPacket message)
    {
        byte[] serializedPacket = SenderInstance(sender).SerializeForWire(message);
        DeliveryMethod deliveryMethod = message.DeliveryMethod;
        byte channel = ChannelOf(message);

        if (receiver == Server.NetPeer)
        {
            DeliverPacket(Server, sender, serializedPacket, deliveryMethod, channel);
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            DeliverPacket(receivingClient, sender, serializedPacket, deliveryMethod, channel);
        }
    }
    public void SendAll(NetPeer sender, IPacket message)
    {
        byte[] serializedPacket = SenderInstance(sender).SerializeForWire(message);
        DeliveryMethod deliveryMethod = message.DeliveryMethod;
        byte channel = ChannelOf(message);

        foreach (NetPeer recipient in GetRecipients(sender))
        {
            DeliverPacket(
                RecipientInstance(recipient),
                sender,
                serializedPacket,
                deliveryMethod,
                channel);
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IPacket message)
    {
        byte[] serializedPacket = SenderInstance(sender).SerializeForWire(message);
        DeliveryMethod deliveryMethod = message.DeliveryMethod;
        byte channel = ChannelOf(message);

        foreach (NetPeer recipient in GetRecipients(sender, ignored))
        {
            DeliverPacket(
                RecipientInstance(recipient),
                sender,
                serializedPacket,
                deliveryMethod,
                channel);
        }
    }

    private EnvironmentInstance SenderInstance(NetPeer sender)
    {
        if (sender == Server.NetPeer) return Server;

        return Clients.Single(client => client.NetPeer == sender);
    }

    private EnvironmentInstance RecipientInstance(NetPeer recipient)
    {
        if (recipient == Server.NetPeer) return Server;

        return Clients.Single(client => client.NetPeer == recipient);
    }

    private void DeliverMessage(EnvironmentInstance recipient, NetPeer sender, byte[] serializedMessage)
    {
        DeliverPayload(
            recipient,
            sender,
            serializedMessage,
            DeliveryMethod.ReliableOrdered,
            WorldChannel);
    }

    private void DeliverPacket(
        EnvironmentInstance recipient,
        NetPeer sender,
        byte[] serializedPacket,
        DeliveryMethod deliveryMethod,
        byte channel)
    {
        DeliverPayload(recipient, sender, serializedPacket, deliveryMethod, channel);
    }

    private void DeliverPayload(
        EnvironmentInstance recipient,
        NetPeer sender,
        byte[] payload,
        DeliveryMethod deliveryMethod,
        byte channel)
    {
        bool markGameThread = ReceiveContext == TestNetworkReceiveContext.GameThread;
        Schedule(
            sender,
            recipient.NetPeer,
            DeliveryDomain(deliveryMethod, channel),
            () => Deliver(() => recipient.SimulateNetworkPayload(
                sender,
                payload,
                markGameThread,
                flushNetworkTick: false)));
    }

    private void Schedule(NetPeer sender, NetPeer receiver, string deliveryDomain, Action delivery)
    {
        scheduler.Schedule(sender, receiver, deliveryDomain, delivery);
        if (AutoDrainReady)
            scheduler.DrainReady();
    }

    private void ValidatePeer(NetPeer peer, string parameterName)
    {
        if (peer == null) throw new ArgumentNullException(parameterName);
        if (peer == Server.NetPeer || Clients.Any(client => client.NetPeer == peer)) return;

        throw new ArgumentException("The peer is not registered with this router", parameterName);
    }

    private static string DeliveryDomain(DeliveryMethod deliveryMethod, byte channel) =>
        $"{deliveryMethod}:channel-{channel}";

    private static byte ChannelOf(IPacket packet) => CoopNetworkBase.GetChannel(packet);

    private static void Deliver(Action delivery)
    {
        // Each E2E instance represents a separate process, so the receiver must not inherit the sender's allowance.
        using (AllowedThread.Suspend())
        {
            delivery();
        }
    }
}
