using Autofac;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using E2E.Tests.Environment.Instance;
using LiteNetLib;

namespace E2E.Tests.Environment;

/// <summary>
/// Network message router for simulating messages across the network
/// </summary>
public class TestNetworkRouter
{
    private ServerInstance Server;
    private List<ClientInstance> Clients = new List<ClientInstance>();

    public void AddServer(ServerInstance instance)
    {
        Server = instance;
    }

    public void AddClient(ClientInstance instance)
    {
        Clients.Add(instance);
    }

    public void Send(NetPeer sender, NetPeer receiver, IMessage message)
    {
        Server.EnsureSerializable(message);

        if (receiver == Server.NetPeer)
        {
            Deliver(() => Server.SimulateMessage(sender, message));
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            Deliver(() => receivingClient.SimulateMessage(sender, message));
        }
    }
    public void SendAll(NetPeer sender, IMessage message)
    {
        Server.EnsureSerializable(message);

        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients)
            {
                Deliver(() => client.SimulateMessage(sender, message));
            }
        }
        else
        {
            Deliver(() => Server.SimulateMessage(sender, message));
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IMessage message)
    {
        Server.EnsureSerializable(message);

        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients.Where(c => c.NetPeer != ignored))
            {
                Deliver(() => client.SimulateMessage(sender, message));
            }
        }
        else
        {
            if (ignored == Server.NetPeer) return;
            Deliver(() => Server.SimulateMessage(sender, message));
        }
    }

    public void Send(NetPeer sender, NetPeer receiver, IPacket message)
    {
        if (receiver == Server.NetPeer)
        {
            Deliver(() => Server.SimulatePacket(sender, message));
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            Deliver(() => receivingClient.SimulatePacket(sender, message));
        }
    }
    public void SendAll(NetPeer sender, IPacket message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients)
            {
                Deliver(() => client.SimulatePacket(sender, message));
            }
        }
        else
        {
            Deliver(() => Server.SimulatePacket(sender, message));
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IPacket message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients.Where(c => c.NetPeer != ignored))
            {
                Deliver(() => client.SimulatePacket(sender, message));
            }
        }
        else
        {
            if (ignored == Server.NetPeer) return;
            Deliver(() => Server.SimulatePacket(sender, message));
        }
    }

    private static void Deliver(Action delivery)
    {
        // Each E2E instance represents a separate process, so the receiver must not inherit the sender's allowance.
        using (AllowedThread.Suspend())
        {
            delivery();
        }
    }
}
