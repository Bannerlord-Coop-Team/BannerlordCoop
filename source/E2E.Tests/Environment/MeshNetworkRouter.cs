using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using GameInterface.Services.Entity;

namespace E2E.Tests.Environment;

/// <summary>
/// Routes mission-mesh traffic between active members of the same in-process mission instance. Delivery uses
/// a virtual-time queue and serialized copies so it follows the same process and wire boundaries as the real mesh.
/// </summary>
public class MeshNetworkRouter
{
    private const string MessageChannel = "message";

    private readonly List<ClientRegistration> clients = new();
    private readonly IVirtualNetworkScheduler scheduler;

    public MeshNetworkRouter(IVirtualNetworkScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        this.scheduler = scheduler;
    }

    public void AddClient(ClientInstance instance, MockBattleNetwork mesh)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(mesh);
        clients.Add(new ClientRegistration(instance, mesh));
    }

    public void Start(MockBattleNetwork mesh)
    {
        RegistrationOf(mesh).IsStarted = true;
    }

    public void ConnectToInstance(MockBattleNetwork mesh, string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) throw new ArgumentException("An instance id is required", nameof(instanceId));

        ClientRegistration registration = RegistrationOf(mesh);
        if (!registration.IsStarted)
            throw new InvalidOperationException("The mesh must be started before connecting to an instance");

        if (registration.InstanceId == instanceId) return;

        scheduler.Cancel(mesh);
        registration.InstanceId = instanceId;
    }

    public void Stop(MockBattleNetwork mesh)
    {
        ClientRegistration registration = RegistrationOf(mesh);
        scheduler.Cancel(mesh);
        registration.InstanceId = null;
        registration.IsStarted = false;
    }

    public void SendAll(MockBattleNetwork sender, IMessage message)
    {
        foreach (ClientRegistration recipient in RecipientsOf(sender))
            ScheduleMessage(sender, recipient, message);
    }

    public void Send(MockBattleNetwork sender, string controllerId, IMessage message)
    {
        foreach (ClientRegistration recipient in RecipientsOf(sender))
            if (ControllerIdOf(recipient.Instance) == controllerId)
                ScheduleMessage(sender, recipient, message);
    }

    public void SendAllBut(MockBattleNetwork sender, string excludedControllerId, IMessage message)
    {
        foreach (ClientRegistration recipient in RecipientsOf(sender))
            if (ControllerIdOf(recipient.Instance) != excludedControllerId)
                ScheduleMessage(sender, recipient, message);
    }

    public void SendAll(MockBattleNetwork sender, IPacket packet)
    {
        foreach (ClientRegistration recipient in RecipientsOf(sender))
            SchedulePacket(sender, recipient, packet);
    }

    public void Send(MockBattleNetwork sender, string controllerId, IPacket packet)
    {
        foreach (ClientRegistration recipient in RecipientsOf(sender))
            if (ControllerIdOf(recipient.Instance) == controllerId)
                SchedulePacket(sender, recipient, packet);
    }

    public void SendAllBut(MockBattleNetwork sender, string excludedControllerId, IPacket packet)
    {
        foreach (ClientRegistration recipient in RecipientsOf(sender))
            if (ControllerIdOf(recipient.Instance) != excludedControllerId)
                SchedulePacket(sender, recipient, packet);
    }

    private IEnumerable<ClientRegistration> RecipientsOf(MockBattleNetwork sender)
    {
        ClientRegistration senderRegistration = RegistrationOf(sender);
        if (!senderRegistration.IsStarted || string.IsNullOrEmpty(senderRegistration.InstanceId))
            return Array.Empty<ClientRegistration>();

        return clients.Where(registration =>
            registration.Mesh != sender &&
            registration.IsStarted &&
            registration.InstanceId == senderRegistration.InstanceId).ToArray();
    }

    private void ScheduleMessage(
        MockBattleNetwork sender,
        ClientRegistration recipient,
        IMessage message)
    {
        IMessage wireCopy = SenderInstance(sender).EnsureSerializable(message);
        scheduler.Schedule(
            sender,
            recipient.Mesh,
            MessageChannel,
            () => Deliver(() => recipient.Instance.SimulateMessage(sender.NetPeer, wireCopy)));
        scheduler.DrainReady();
    }

    private void SchedulePacket(
        MockBattleNetwork sender,
        ClientRegistration recipient,
        IPacket packet)
    {
        IPacket wireCopy = SenderInstance(sender).EnsureSerializable(packet);
        scheduler.Schedule(
            sender,
            recipient.Mesh,
            $"packet:{packet.DeliveryMethod}",
            () => Deliver(() => recipient.Instance.SimulatePacket(sender.NetPeer, wireCopy)));
        scheduler.DrainReady();
    }

    private ClientInstance SenderInstance(MockBattleNetwork sender) => RegistrationOf(sender).Instance;

    private ClientRegistration RegistrationOf(MockBattleNetwork mesh)
    {
        ClientRegistration? registration = clients.FirstOrDefault(client => client.Mesh == mesh);
        if (registration == null) throw new InvalidOperationException("The mesh is not registered with this router");
        return registration;
    }

    private static void Deliver(Action delivery)
    {
        // Each E2E instance represents a separate process, so the receiver must not inherit the sender's allowance.
        using (AllowedThread.Suspend())
        {
            delivery();
        }
    }

    private static string ControllerIdOf(ClientInstance instance) =>
        instance.Resolve<IControllerIdProvider>().ControllerId;

    private sealed class ClientRegistration
    {
        public ClientInstance Instance { get; }
        public MockBattleNetwork Mesh { get; }
        public bool IsStarted { get; set; }
        public string? InstanceId { get; set; }

        public ClientRegistration(ClientInstance instance, MockBattleNetwork mesh)
        {
            Instance = instance;
            Mesh = mesh;
        }
    }
}
