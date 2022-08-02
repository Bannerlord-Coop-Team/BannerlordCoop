using System.Collections.Generic;
using System.Linq;
using Coop.Mod.Serializers;
using JetBrains.Annotations;
using Sync.Store;

namespace Coop.Tests.Sync
{
    public class TestStores
    {
        public TestStores( TestConnections connections)
        {
            Connections = connections;

            foreach ((ConnectionTestImpl client, ConnectionTestImpl server) con in connections
                .ConnectionsClient
                .Zip(connections.ConnectionsServer, (c, s) => (c, s))
            )
            {
                StoresClient.Add(new RemoteStoreClient(con.client,new SerializableFactory()));
                StoreServer.AddConnection(con.server);
            }
        }

        public List<RemoteStoreClient> StoresClient { get; } = new List<RemoteStoreClient>();

        public RemoteStoreServer StoreServer { get; } =
            new RemoteStoreServer(new SerializableFactory());

        public TestConnections Connections { get; }
    }
}