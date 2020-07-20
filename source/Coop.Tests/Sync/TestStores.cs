using System.Collections.Generic;
using System.Linq;
using Coop.Mod.Serializers;
using JetBrains.Annotations;
using Sync.Store;

namespace Coop.Tests.Sync
{
    public class TestStores
    {
        public TestStores([NotNull] TestConnections connections)
        {
            Connections = connections;

            foreach ((ConnectionTestImpl client, ConnectionTestImpl server) con in connections
                                                                                   .ConnectionsClient
                                                                                   .Zip(
                                                                                       connections
                                                                                           .ConnectionsServer)
            )
            {
                StoresClient.Add(
                    new RemoteStore(
                        new Dictionary<ObjectId, object>(),
                        con.client,
                        new SerializableFactory()));
                StoreServer.AddConnection(con.server);
            }
        }

        public List<RemoteStore> StoresClient { get; } = new List<RemoteStore>();

        public SharedRemoteStore StoreServer { get; } =
            new SharedRemoteStore(new SerializableFactory());

        public TestConnections Connections { get; }
    }
}
