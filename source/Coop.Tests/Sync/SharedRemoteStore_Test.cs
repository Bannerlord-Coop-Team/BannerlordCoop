using Sync.Store;
using Xunit;

namespace Coop.Tests.Sync
{
    public class SharedRemoteStore_Test
    {
        private readonly TestStores m_TestStores = new TestStores();

        [Fact]
        private void DataIsBroadcastToOtherClients()
        {
            string message = "Hello World";
            m_TestStores.Init(2);

            RemoteStore client0 = m_TestStores.StoresClient[0];
            RemoteStore client1 = m_TestStores.StoresClient[1];

            ObjectId id = client0.Insert(message);
            Assert.Contains(id, client0.Data);
            Assert.DoesNotContain(id, client1.Data);

            // Client0 Add -> Server
            m_TestStores.ExecuteSendsClients();
            Assert.DoesNotContain(id, client1.Data);
            Assert.Contains(id, m_TestStores.StoreServer.Data);
            Assert.IsType<string>(m_TestStores.StoreServer.Data[id]);
            Assert.Equal(message, m_TestStores.StoreServer.Data[id] as string);

            // Server Add -> Client1
            m_TestStores.ExecuteSendsServer();
            Assert.Contains(id, client1.Data);
            Assert.IsType<string>(client1.Data[id]);
            Assert.Equal(message, client1.Data[id] as string);

            // Client1 ACK -> Server
            m_TestStores.ExecuteSendsClients();
            Assert.False(client0.State[id].Acknowledged);
            // Server ACK -> Client 0
            m_TestStores.ExecuteSendsServer();
            Assert.True(client0.State[id].Acknowledged);

            // Object is present and equal in all stores
            Assert.Equal(message, client0.Data[id] as string);
            Assert.Equal(message, client1.Data[id] as string);
            Assert.Equal(message, m_TestStores.StoreServer.Data[id] as string);
        }

        [Fact]
        private void DataIsReceivedFromClient()
        {
            string message = "Hello World";
            m_TestStores.Init(1);

            RemoteStore client0 = m_TestStores.StoresClient[0];

            ObjectId id = client0.Insert(message);
            Assert.DoesNotContain(id, m_TestStores.StoreServer.Data);

            m_TestStores.ExecuteSendsClients();
            Assert.Contains(id, m_TestStores.StoreServer.Data);
            Assert.IsType<string>(m_TestStores.StoreServer.Data[id]);
            Assert.Equal(message, m_TestStores.StoreServer.Data[id] as string);
        }

        [Fact]
        private void OnDistributedIsInvoked()
        {
            string message = "Hello World";
            m_TestStores.Init(2);

            RemoteStore client0 = m_TestStores.StoresClient[0];
            RemoteStore client1 = m_TestStores.StoresClient[1];

            ObjectId id = client0.Insert(message);

            ObjectId? handlerArgument = null;
            m_TestStores.StoreServer.OnObjectDistributed += objectId =>
            {
                handlerArgument = objectId;
            };

            // Client0 Add -> Server
            m_TestStores.ExecuteSendsClients();
            Assert.False(handlerArgument.HasValue);

            // Server Add -> Client1
            m_TestStores.ExecuteSendsServer();
            Assert.False(handlerArgument.HasValue);

            // Client1 ACK -> Server
            m_TestStores.ExecuteSendsClients();
            Assert.True(handlerArgument.HasValue);

            // Server ACK -> Client 0
            m_TestStores.ExecuteSendsServer();
        }

        [Fact]
        private void ServerAckIsDelayedWithMultipleClients()
        {
            string message = "Hello World";
            m_TestStores.Init(2);

            RemoteStore client0 = m_TestStores.StoresClient[0];
            ObjectId id = client0.Insert(message);
            Assert.True(client0.State[id].Sent);
            Assert.False(client0.State[id].Acknowledged);

            // Client0 Add -> Server
            m_TestStores.ExecuteSendsClients();
            Assert.False(client0.State[id].Acknowledged);

            // Server Add -> Client1
            m_TestStores.ExecuteSendsServer();
            RemoteStore client1 = m_TestStores.StoresClient[1];
            Assert.Contains(id, client1.State);
            Assert.False(
                client0.State[id]
                       .Acknowledged); // Delayed until client 1 ACK is processed by server

            // Client1 ACK -> Server
            m_TestStores.ExecuteSendsClients();
            Assert.False(
                client0.State[id]
                       .Acknowledged); // Delayed until client 1 ACK is processed by server

            // Server ACK -> Client 0
            m_TestStores.ExecuteSendsServer();
            Assert.True(
                client0.State[id]
                       .Acknowledged); // Delayed until client 1 ACK is processed by server
        }

        [Fact]
        private void ServerCanAddObject()
        {
            string message = "Hello World";
            m_TestStores.Init(2);

            RemoteStore client0 = m_TestStores.StoresClient[0];
            RemoteStore client1 = m_TestStores.StoresClient[1];

            ObjectId id = m_TestStores.StoreServer.Insert(message);
            Assert.Contains(id, m_TestStores.StoreServer.Data);
            Assert.DoesNotContain(id, client0.Data);
            Assert.DoesNotContain(id, client1.Data);

            m_TestStores.ExecuteSendsServer();
            Assert.Contains(id, m_TestStores.StoreServer.Data);
            Assert.Contains(id, client0.Data);
            Assert.Contains(id, client1.Data);
        }

        [Fact]
        private void ServerSendsAckWithOneClient()
        {
            string message = "Hello World";
            m_TestStores.Init(1);

            RemoteStore client0 = m_TestStores.StoresClient[0];
            ObjectId id = client0.Insert(message);
            Assert.True(client0.State[id].Sent);
            Assert.False(client0.State[id].Acknowledged);

            // Client0 Add -> Server
            m_TestStores.ExecuteSendsClients();
            Assert.False(client0.State[id].Acknowledged);

            // Server ACK -> Client0
            m_TestStores.ExecuteSendsServer();
            Assert.True(client0.State[id].Acknowledged);
        }
    }
}
