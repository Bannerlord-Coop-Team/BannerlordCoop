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
            Server.SendMessageInternal(sender, message);
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            receivingClient.SendMessageInternal(sender, message);
        }
    }
    public void SendAll(NetPeer sender, IMessage message)
    {
        if (sender == Server.NetPeer)
        {
            foreach(var client in Clients)
            {
                client.SendMessageInternal(sender, message);
            }
        }
        else
        {
            Server.SendMessageInternal(sender, message);
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IMessage message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients.Where(c => c.NetPeer != ignored))
            {
                client.SendMessageInternal(sender, message);
            }
        }
        else
        {
            if (ignored == Server.NetPeer) return;
            Server.SendMessageInternal(sender, message);
        }
    }

    public void Send(NetPeer sender, NetPeer receiver, IPacket message)
    {
        if (receiver == Server.NetPeer)
        {
            Server.SendPacketInternal(sender, message);
        }
        else
        {
            var receivingClient = Clients.Single(client => client.NetPeer == receiver);

            receivingClient.SendPacketInternal(sender, message);
        }
    }
    public void SendAll(NetPeer sender, IPacket message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients)
            {
                client.SendPacketInternal(sender, message);
            }
        }
        else
        {
            Server.SendPacketInternal(sender, message);
        }
    }

    public void SendAllBut(NetPeer sender, NetPeer ignored, IPacket message)
    {
        if (sender == Server.NetPeer)
        {
            foreach (var client in Clients.Where(c => c.NetPeer != ignored))
            {
                client.SendPacketInternal(sender, message);
            }
        }
        else
        {
            if (ignored == Server.NetPeer) return;
            Server.SendPacketInternal(sender, message);
        }
    }
}
