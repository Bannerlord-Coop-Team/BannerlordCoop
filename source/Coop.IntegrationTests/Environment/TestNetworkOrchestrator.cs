using Common.Messaging;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment;

public class TestNetworkOrchestrator
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
}
