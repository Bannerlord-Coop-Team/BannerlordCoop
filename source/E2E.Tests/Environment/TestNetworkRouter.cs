using Autofac;
using Common.Messaging;
using Common.PacketHandlers;
using E2E.Tests.Environment.Instance;
using LiteNetLib;
using ProtoBuf.Meta;

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
        Server.EnsureSerializable(message);

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
        Server.EnsureSerializable(message);

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
