using System.Collections.Generic;
using Coop.Mod.Serializers;
using Network.Infrastructure;
using Sync.Store;

namespace Coop.Tests.Sync
{
    public class TestStores
    {
        public List<ConnectionTestImpl> ConnectionsClient { get; } = new List<ConnectionTestImpl>();

        public List<RemoteStore> StoresClient { get; } = new List<RemoteStore>();

        public List<ConnectionTestImpl> ConnectionsServer { get; } = new List<ConnectionTestImpl>();

        public SharedRemoteStore StoreServer { get; } =
            new SharedRemoteStore(new SerializableFactory());

        public void Init(int iNumberOfClients)
        {
            for (int i = 0; i < iNumberOfClients; ++i)
            {
                ConnectionTestImpl client = new ConnectionTestImpl
                {
                    StateImpl = EConnectionState.ClientPlaying
                };
                ConnectionTestImpl server = new ConnectionTestImpl
                {
                    StateImpl = EConnectionState.ServerPlaying
                };

                client.NetworkImpl.OnSend += server.Receive;
                server.NetworkImpl.OnSend += client.Receive;
                StoresClient.Add(
                    new RemoteStore(
                        new Dictionary<ObjectId, object>(),
                        client,
                        new SerializableFactory()));
                StoreServer.AddConnection(server);

                ConnectionsClient.Add(client);
                ConnectionsServer.Add(server);
            }
        }

        public void ExecuteSendsClients()
        {
            ConnectionsClient.ForEach(c => c.NetworkImpl.ExecuteSends());
        }

        public void ExecuteSendsServer()
        {
            ConnectionsServer.ForEach(c => c.NetworkImpl.ExecuteSends());
        }
    }
}
