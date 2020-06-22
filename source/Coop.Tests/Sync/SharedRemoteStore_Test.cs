using System.Collections.Generic;
using Network.Infrastructure;
using Sync.Store;
using Xunit;

namespace Coop.Tests.Sync
{
    public class SharedRemoteStore_Test
    {
        private readonly List<ConnectionTestImpl> m_ConnectionsClient =
            new List<ConnectionTestImpl>();

        private readonly List<RemoteStore> m_StoresClient = new List<RemoteStore>();

        private readonly List<ConnectionTestImpl> m_ConnectionsServer =
            new List<ConnectionTestImpl>();

        private readonly SharedRemoteStore m_StoreServer = new SharedRemoteStore();

        private void Init(int iNumberOfClients)
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
                m_StoresClient.Add(new RemoteStore(new Dictionary<ObjectId, object>(), client));
                m_StoreServer.AddConnection(server);

                m_ConnectionsClient.Add(client);
                m_ConnectionsServer.Add(server);
            }
        }

        private void ExecuteSendsClients()
        {
            m_ConnectionsClient.ForEach(c => c.NetworkImpl.ExecuteSends());
        }

        private void ExecuteSendsServer()
        {
            m_ConnectionsServer.ForEach(c => c.NetworkImpl.ExecuteSends());
        }

        [Fact]
        private void DataIsBroadcastToOtherClients()
        {
            string message = "Hello World";
            Init(2);

            RemoteStore client0 = m_StoresClient[0];
            RemoteStore client1 = m_StoresClient[1];

            ObjectId id = client0.Insert(message);
            Assert.Contains(id, client0.Data);
            Assert.DoesNotContain(id, client1.Data);

            // Client0 Add -> Server
            ExecuteSendsClients();
            Assert.DoesNotContain(id, client1.Data);
            Assert.Contains(id, m_StoreServer.Data);
            Assert.IsType<string>(m_StoreServer.Data[id]);
            Assert.Equal(message, m_StoreServer.Data[id] as string);

            // Server Add -> Client1
            ExecuteSendsServer();
            Assert.Contains(id, client1.Data);
            Assert.IsType<string>(client1.Data[id]);
            Assert.Equal(message, client1.Data[id] as string);

            // Client1 ACK -> Server
            ExecuteSendsClients();
            // Server ACK -> Client 0
            ExecuteSendsClients();

            // Object is present and equal in all stores
            Assert.Equal(message, client0.Data[id] as string);
            Assert.Equal(message, client1.Data[id] as string);
            Assert.Equal(message, m_StoreServer.Data[id] as string);
        }

        [Fact]
        private void DataIsReceivedFromClient()
        {
            string message = "Hello World";
            Init(1);

            RemoteStore client0 = m_StoresClient[0];

            ObjectId id = client0.Insert(message);
            Assert.DoesNotContain(id, m_StoreServer.Data);

            ExecuteSendsClients();
            Assert.Contains(id, m_StoreServer.Data);
            Assert.IsType<string>(m_StoreServer.Data[id]);
            Assert.Equal(message, m_StoreServer.Data[id] as string);
        }

        [Fact]
        private void ServerAckIsDelayedWithMultipleClients()
        {
            string message = "Hello World";
            Init(2);

            RemoteStore client0 = m_StoresClient[0];
            ObjectId id = client0.Insert(message);
            Assert.True(client0.State[id].Sent);
            Assert.False(client0.State[id].Acknowledged);

            // Client0 Add -> Server
            ExecuteSendsClients();
            Assert.False(client0.State[id].Acknowledged);

            // Server Add -> Client1
            ExecuteSendsServer();
            RemoteStore client1 = m_StoresClient[1];
            Assert.Contains(id, client1.State);
            Assert.False(
                client0.State[id]
                       .Acknowledged); // Delayed until client 1 ACK is processed by server

            // Client1 ACK -> Server
            ExecuteSendsClients();
            Assert.False(
                client0.State[id]
                       .Acknowledged); // Delayed until client 1 ACK is processed by server

            // Server ACK -> Client 0
            ExecuteSendsServer();
            Assert.True(
                client0.State[id]
                       .Acknowledged); // Delayed until client 1 ACK is processed by server
        }

        [Fact]
        private void ServerCanAddObject()
        {
            string message = "Hello World";
            Init(2);

            RemoteStore client0 = m_StoresClient[0];
            RemoteStore client1 = m_StoresClient[1];

            ObjectId id = m_StoreServer.Insert(message);
            Assert.Contains(id, m_StoreServer.Data);
            Assert.DoesNotContain(id, client0.Data);
            Assert.DoesNotContain(id, client1.Data);

            ExecuteSendsServer();
            Assert.Contains(id, m_StoreServer.Data);
            Assert.Contains(id, client0.Data);
            Assert.Contains(id, client1.Data);
        }

        [Fact]
        private void ServerSendsAckWithOneClient()
        {
            string message = "Hello World";
            Init(1);

            RemoteStore client0 = m_StoresClient[0];
            ObjectId id = client0.Insert(message);
            Assert.True(client0.State[id].Sent);
            Assert.False(client0.State[id].Acknowledged);

            // Client0 Add -> Server
            ExecuteSendsClients();
            Assert.False(client0.State[id].Acknowledged);

            // Server ACK -> Client0
            ExecuteSendsServer();
            Assert.True(client0.State[id].Acknowledged);
        }
    }
}
