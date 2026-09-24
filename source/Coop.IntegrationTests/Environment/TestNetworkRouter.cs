using Autofac;
using Common.Messaging;
using Common.PacketHandlers;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.ObjectManager;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment;

/// <summary>
/// Network message router for simulating messages across the network
/// </summary>
public class TestNetworkRouter
{
    public bool IsMessageRoutingEnabled { get; set; } = true;

    private ServerInstance Server;
    private List<ClientInstance> Clients = new List<ClientInstance>();
    private readonly object handleGate = new object();
    private readonly Dictionary<string, uint> fixtureHandles = new Dictionary<string, uint>();

    public void AddServer(ServerInstance instance)
    {
        Server = instance;
    }

    public void AddClient(ClientInstance instance)
    {
        Clients.Add(instance);
    }

    public uint GetOrCreateFixtureHandle(string stringId)
    {
        lock (handleGate)
        {
            if (fixtureHandles.TryGetValue(stringId, out var existingHandle))
                return existingHandle;

            uint handle = 0;
            Server.Call(() =>
            {
                var reservation = new object();
                IObjectManager objectManager = Server.ObjectManager;
                if (!objectManager.AddNewObject(reservation, out _) ||
                    !objectManager.TryGetHandle(reservation, out handle) ||
                    !objectManager.Remove(reservation))
                {
                    throw new InvalidOperationException($"Unable to reserve a network handle for {stringId}");
                }
            });

            fixtureHandles.Add(stringId, handle);
            return handle;
        }
    }

    public void Send(NetPeer sender, NetPeer receiver, IMessage message)
    {
        if (!IsMessageRoutingEnabled) return;

        if (receiver == Server.NetPeer)
        {
            Server.SimulateMessage(sender, message);
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            receivingClient.SimulateMessage(sender, message);
        }
    }
    public void SendAll(NetPeer sender, IMessage message)
    {
        if (!IsMessageRoutingEnabled) return;

        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients)
            {
                client.SimulateMessage(sender, message);
            }
        }
        else
        {
            Server.SimulateMessage(sender, message);
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IMessage message)
    {
        if (!IsMessageRoutingEnabled) return;

        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients.Where(c => c.NetPeer != ignored))
            {
                client.SimulateMessage(sender, message);
            }
        }
        else
        {
            if (ignored == Server.NetPeer) return;
            Server.SimulateMessage(sender, message);
        }
    }

    public void Send(NetPeer sender, NetPeer receiver, IPacket message)
    {
        if (receiver == Server.NetPeer)
        {
            Server.SimulatePacket(sender, message);
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            receivingClient.SimulatePacket(sender, message);
        }
    }
    public void SendAll(NetPeer sender, IPacket message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients)
            {
                client.SimulatePacket(sender, message);
            }
        }
        else
        {
            Server.SimulatePacket(sender, message);
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IPacket message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients.Where(c => c.NetPeer != ignored))
            {
                client.SimulatePacket(sender, message);
            }
        }
        else
        {
            if (ignored == Server.NetPeer) return;
            Server.SimulatePacket(sender, message);
        }
    }
}
