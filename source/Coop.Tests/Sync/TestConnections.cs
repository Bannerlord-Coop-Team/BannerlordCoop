using System.Collections.Generic;
using System.Linq;
using Coop.NetImpl.LiteNet;
using Network.Infrastructure;

namespace Coop.Tests.Sync
{
    public class TestConnectionsRaw
    {
        public TestConnectionsRaw(int iNumberOfClients)
        {
            for (int i = 0; i < iNumberOfClients; ++i)
            {
                ConnectionsClient.Add(new InMemoryConnection());
                ConnectionsServer.Add(new InMemoryConnection());
            }
        }

        public List<InMemoryConnection> ConnectionsClient { get; } = new List<InMemoryConnection>();
        public List<InMemoryConnection> ConnectionsServer { get; } = new List<InMemoryConnection>();

        public void ExecuteSendsClients()
        {
            ConnectionsClient.ForEach(c => c.ExecuteSends());
        }

        public void ExecuteSendsServer()
        {
            ConnectionsServer.ForEach(c => c.ExecuteSends());
        }
    }

    public class TestConnections
    {
        public TestConnections(TestConnectionsRaw raw, bool bSetupPersistence)
        {
            foreach ((InMemoryConnection client, InMemoryConnection server) con in raw
                                                                                   .ConnectionsClient
                                                                                   .Zip(
                                                                                       raw
                                                                                           .ConnectionsServer)
            )
            {
                IGameStatePersistence persistenceClient = bSetupPersistence ?
                    new RailNetPeerWrapper(con.client) :
                    (IGameStatePersistence) new GameStatePersistenceTestImpl();
                ConnectionTestImpl client = new ConnectionTestImpl(
                    ConnectionTestImpl.EType.Client,
                    con.client,
                    persistenceClient)
                {
                    StateImpl = EClientConnectionState.Connected
                };

                IGameStatePersistence persistenceServer = bSetupPersistence ?
                    new RailNetPeerWrapper(con.server) :
                    (IGameStatePersistence) new GameStatePersistenceTestImpl();
                ConnectionTestImpl server = new ConnectionTestImpl(
                    ConnectionTestImpl.EType.Server,
                    con.server,
                    persistenceServer)
                {
                    StateImpl = EServerConnectionState.Ready
                };

                con.client.OnSend += server.Receive;
                con.server.OnSend += client.Receive;

                ConnectionsClient.Add(client);
                ConnectionsServer.Add(server);
            }
        }

        public List<ConnectionTestImpl> ConnectionsClient { get; } = new List<ConnectionTestImpl>();
        public List<ConnectionTestImpl> ConnectionsServer { get; } = new List<ConnectionTestImpl>();
    }
}
