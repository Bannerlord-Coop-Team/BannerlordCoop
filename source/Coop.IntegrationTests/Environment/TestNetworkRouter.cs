using Common.Messaging;
using Common.PacketHandlers;
using Coop.IntegrationTests.Environment.Instance;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment;

/// <summary>
/// Network message router for simulating messages across the network
/// </summary>
internal class TestNetworkRouter
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
        if(receiver == Server.NetPeer)
        {
            Server.ReceiveMessage(sender, message);
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            receivingClient.ReceiveMessage(sender, message);
        }
    }
    public void SendAll(NetPeer sender, IMessage message)
    {
        if (sender == Server.NetPeer)
        {
            foreach(var client in Clients)
            {
                client.ReceiveMessage(sender, message);
            }
        }
        else
        {
            Server.ReceiveMessage(sender, message);
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IMessage message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients.Where(c => c.NetPeer != ignored))
            {
                client.ReceiveMessage(sender, message);
            }
        }
        else
        {
            if (ignored == Server.NetPeer) return;
            Server.ReceiveMessage(sender, message);
        }
    }

    public void Send(NetPeer sender, NetPeer receiver, IPacket message)
    {
        if (receiver == Server.NetPeer)
        {
            Server.ReceivePacket(sender, message);
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            receivingClient.ReceivePacket(sender, message);
        }
    }
    public void SendAll(NetPeer sender, IPacket message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients)
            {
                client.ReceivePacket(sender, message);
            }
        }
        else
        {
            Server.ReceivePacket(sender, message);
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IPacket message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients.Where(c => c.NetPeer != ignored))
            {
                client.ReceivePacket(sender, message);
            }
        }
        else
        {
            if (ignored == Server.NetPeer) return;
            Server.ReceivePacket(sender, message);
        }
    }
}
