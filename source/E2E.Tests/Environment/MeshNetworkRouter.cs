using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using GameInterface.Services.Entity;

namespace E2E.Tests.Environment;

/// <summary>
/// Routes mission-mesh (<see cref="Missions.IBattleNetwork"/>) <see cref="IMessage"/> traffic between client
/// instances in-process — the mesh counterpart to <see cref="TestNetworkRouter"/>. The mesh is
/// client-to-client with no server, so this knows only about clients and addresses them by their
/// controller id. Delivery serializes the message first (matching the real wire) and publishes it on the
/// receiving client's broker, exactly as a received mesh packet would.
/// </summary>
public class MeshNetworkRouter
{
    private readonly List<(ClientInstance instance, MockBattleNetwork mesh)> clients = new();

    public void AddClient(ClientInstance instance, MockBattleNetwork mesh) => clients.Add((instance, mesh));

    public void SendAll(MockBattleNetwork sender, IMessage message)
    {
        SenderInstance(sender).EnsureSerializable(message);

        foreach (var (instance, mesh) in clients)
            if (mesh != sender)
                instance.SimulateMessage(sender.NetPeer, message);
    }

    public void Send(MockBattleNetwork sender, string controllerId, IMessage message)
    {
        SenderInstance(sender).EnsureSerializable(message);

        foreach (var (instance, mesh) in clients)
            if (mesh != sender && ControllerIdOf(instance) == controllerId)
                instance.SimulateMessage(sender.NetPeer, message);
    }

    public void SendAllBut(MockBattleNetwork sender, string excludedControllerId, IMessage message)
    {
        SenderInstance(sender).EnsureSerializable(message);

        foreach (var (instance, mesh) in clients)
            if (mesh != sender && ControllerIdOf(instance) != excludedControllerId)
                instance.SimulateMessage(sender.NetPeer, message);
    }

    private ClientInstance SenderInstance(MockBattleNetwork sender)
        => clients.First(c => c.mesh == sender).instance;

    private static string ControllerIdOf(ClientInstance instance)
        => instance.Resolve<IControllerIdProvider>().ControllerId;
}
