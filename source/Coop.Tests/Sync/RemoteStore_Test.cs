using System.Collections.Generic;
using Network.Infrastructure;
using Sync.Store;
using Xunit;

namespace Coop.Tests.Sync
{
    public class RemoteStore_Test
    {
        private readonly List<ConnectionTestImpl> m_ConnectionsClient =
            new List<ConnectionTestImpl>();

        private readonly List<RemoteStore> m_StoresClient = new List<RemoteStore>();

        private readonly List<ConnectionTestImpl> m_ConnectionsServer =
            new List<ConnectionTestImpl>();

        private readonly List<RemoteStore> m_StoresServer = new List<RemoteStore>();

        private void Init(int iNumberOfClients)
        {
            Dictionary<ObjectId, RemoteObject> dataServer =
                new Dictionary<ObjectId, RemoteObject>();
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
                m_StoresClient.Add(
                    new RemoteStore(new Dictionary<ObjectId, RemoteObject>(), client));
                m_StoresServer.Add(new RemoteStore(dataServer, server));

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
        private void DataIsReceived()
        {
            string message = "Hello World";
            Init(1);

            RemoteStore clientStore = m_StoresClient[0];
            RemoteStore serverStore = m_StoresServer[0];

            ObjectId id = clientStore.Insert(message);
            Assert.DoesNotContain(id, serverStore.Data);
            ExecuteSendsClients();
            Assert.Contains(id, serverStore.Data);
            Assert.IsType<string>(serverStore.Data[id].Object);
            Assert.Equal(message, serverStore.Data[id].Object as string);
        }

        [Fact]
        private void OnAcknowledgedIsTriggered()
        {
            string message = "Hello World";
            Init(1);

            RemoteStore clientStore = m_StoresClient[0];
            RemoteStore serverStore = m_StoresServer[0];

            ObjectId expectedId = clientStore.Insert(message);
            ExecuteSendsClients();

            bool wasCallbackCalled = false;
            clientStore.OnObjectAcknowledged += (acknowledgedId, acknowledgedObject) =>
            {
                wasCallbackCalled = true;
                Assert.Equal(expectedId, acknowledgedId);
                Assert.Equal(message, acknowledgedObject);
            };
            ExecuteSendsServer();

            Assert.True(wasCallbackCalled);
        }

        [Fact]
        private void OnReceivedIsTriggered()
        {
            string message = "Hello World";
            Init(1);

            RemoteStore clientStore = m_StoresClient[0];
            RemoteStore serverStore = m_StoresServer[0];

            ObjectId expectedId = clientStore.Insert(message);

            bool wasCallbackCalled = false;
            serverStore.OnObjectReceived += (receivedId, receivedObject) =>
            {
                wasCallbackCalled = true;
                Assert.Equal(expectedId, receivedId);
                Assert.Equal(message, receivedObject);
            };
            ExecuteSendsClients();
            Assert.True(wasCallbackCalled);
        }

        [Fact]
        private void ReceiveIsAcknowledged()
        {
            string message = "Hello World";
            Init(1);

            RemoteStore clientStore = m_StoresClient[0];
            RemoteStore serverStore = m_StoresServer[0];

            ObjectId id = clientStore.Insert(message);
            Assert.Contains(id, clientStore.Data);
            Assert.True(clientStore.Data[id].Sent);
            Assert.False(clientStore.Data[id].Acknowledged);
            ExecuteSendsClients();
            Assert.Contains(id, serverStore.Data);
            Assert.True(serverStore.Data[id].Acknowledged);
            Assert.False(serverStore.Data[id].Sent);
            Assert.False(
                clientStore.Data[id].Acknowledged); // Server sends have not been processed yet!
            ExecuteSendsServer();
            Assert.True(clientStore.Data[id].Acknowledged);
        }
    }
}
