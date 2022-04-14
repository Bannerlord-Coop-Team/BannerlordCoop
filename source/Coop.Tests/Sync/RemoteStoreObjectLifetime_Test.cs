using Coop.Tests.Persistence;
using Network;
using Network.Protocol;
using Sync.Store;
using System;
using System.Linq;
using Xunit;

namespace Coop.Tests.Sync
{
    public class RemoteStoreObjectLifetime_Test
    {
        [Fact]
        private void ServerCanAddObject()
        {
            var env = new TestEnvironment(2);
            var message = "Hello World";

            var client0 = env.StoresClient[0];
            var client1 = env.StoresClient[1];
            var server = env.StoreServer;

            var id = env.StoreServer.Insert(message);
            Assert.Contains(id, env.StoreServer.Data);
            Assert.DoesNotContain(id, client0.Data);
            Assert.DoesNotContain(id, client1.Data);

            env.ExecuteSendsServer();
            Assert.Contains(id, env.StoreServer.Data);
            Assert.Contains(id, client0.Data);
            Assert.Contains(id, client1.Data);

            // The object is tracked only in server, because only there it was added through `Insert`.
            Assert.Single(server.State);
            var sharedState = server.State.First().Value;
            Assert.Equal(1u, sharedState.InsertCountServer);
            Assert.True(sharedState.RemoteState.ContainsKey(env.Connections.ConnectionsServer[0]));
            Assert.True(sharedState.RemoteState.ContainsKey(env.Connections.ConnectionsServer[1]));
            foreach(var clientState in sharedState.RemoteState.Values)
            {
                Assert.Equal(0u, clientState.InsertCount);
            }
        }

        [Fact]
        private void ClientsCanAddObjects()
        {
            var env = new TestEnvironment(2);
            var message = "Hello World";

            var client0 = env.StoresClient[0];
            var client1 = env.StoresClient[1];
            var server = env.StoreServer;

            var id = client0.Insert(message);
            Assert.Contains(id, client0.Data);
            Assert.DoesNotContain(id, client1.Data);

            // Client0 Add -> Server
            env.ExecuteSendsClients();
            Assert.DoesNotContain(id, client1.Data);
            Assert.Contains(id, env.StoreServer.Data);
            Assert.IsType<string>(env.StoreServer.Data[id]);
            Assert.Equal(message, env.StoreServer.Data[id] as string);

            // Server Add -> Client1
            env.ExecuteSendsServer();
            Assert.Contains(id, client1.Data);
            Assert.IsType<string>(client1.Data[id]);
            Assert.Equal(message, client1.Data[id] as string);

            // Client1 ACK -> Server
            env.ExecuteSendsClients();

            // Server ACK -> Client 0
            env.ExecuteSendsServer();

            // Object is present and equal in all stores
            Assert.Equal(message, client0.Data[id] as string);
            Assert.Equal(message, client1.Data[id] as string);
            Assert.Equal(message, env.StoreServer.Data[id] as string);

            // The object was inserted in client0
            Assert.Single(server.State);
            var sharedState = server.State.First().Value;
            Assert.Equal(0u, sharedState.InsertCountServer);
            Assert.True(sharedState.RemoteState.ContainsKey(env.Connections.ConnectionsServer[0]));
            Assert.True(sharedState.RemoteState.ContainsKey(env.Connections.ConnectionsServer[1]));
            Assert.Equal(1u, sharedState.RemoteState[env.Connections.ConnectionsServer[0]].InsertCount);
            Assert.Equal(0u, sharedState.RemoteState[env.Connections.ConnectionsServer[1]].InsertCount);
        }

        [Fact]
        private void ServerCanRemoveObjects()
        {
            // Sync an object to all stores
            var env = new TestEnvironment(2);
            var obj = "Hello World";
            var client0 = env.StoresClient[0];
            var client1 = env.StoresClient[1];
            RemoteStoreServer server = env.StoreServer;
            ObjectId id = env.StoreServer.Insert(obj);
            Assert.Single(server.Data);
            env.ExecuteSendsServer();
            env.ExecuteSendsClients();
            Assert.Single(server.State);
            var sharedState = server.State.First().Value;
            Assert.Equal(1u, sharedState.InsertCountServer);

            // Retrieve & remove the object
            bool callbackCalled = false;
            server.OnObjectRetrieved = (objectId, shared) =>
            {
                callbackCalled = true;
                Assert.Equal(1u, shared.InsertCountServer);
                Assert.Equal(2, shared.RemoteState.Count);
                foreach (var clientState in shared.RemoteState.Values)
                {
                    Assert.Equal(0u, clientState.InsertCount);
                }
                Assert.Equal(1u, shared.RetrieveCountServer);
                return true;
            };
            object retrievedObject = server.Retrieve(id);

            // Server has alreadt deleted objects locally
            Assert.True(callbackCalled);
            Assert.Empty(server.State);
            Assert.Empty(server.Data);

            // Server is sending out StoreRemove to all clients
            Assert.Single(env.ConnectionsRaw.ConnectionsServer[0].SendBuffer);
            AssertIsStorePacket(env.ConnectionsRaw.ConnectionsServer[0].SendBuffer[0], EPacket.StoreRemove, id);
            Assert.Single(env.ConnectionsRaw.ConnectionsServer[1].SendBuffer);
            AssertIsStorePacket(env.ConnectionsRaw.ConnectionsServer[1].SendBuffer[0], EPacket.StoreRemove, id);

            Assert.Single(client0.Data);
            Assert.Single(client1.Data);
            env.ExecuteSendsServer();
            Assert.Empty(client0.Data);
            Assert.Empty(client1.Data);
        }

        [Fact]
        private void ClientsSendRetrieve()
        {
            // Sync an object to all stores
            var env = new TestEnvironment(2);
            var message = "Hello World";
            var client0 = env.StoresClient[0];
            var client1 = env.StoresClient[1];
            var server = env.StoreServer;
            var id = client0.Insert(message);
            Assert.Single(client0.Data);
            env.ExecuteSendsClients(); // ADD client 0 -> server
            env.ExecuteSendsServer();  // Server Add -> Client1
            env.ExecuteSendsClients(); // Client1 ACK -> Server
            env.ExecuteSendsServer();  // Server ACK -> Client 0

            // Object is present and equal in all stores
            Assert.Equal(message, client0.Data[id] as string);
            Assert.Equal(message, client1.Data[id] as string);
            Assert.Equal(message, env.StoreServer.Data[id] as string);

            // Setup retreive handler
            int CallbackCounter = 0;
            server.OnObjectRetrieved = (objectId, state) =>
            {
                CallbackCounter++;
                return false;
            };

            // Retrieve it on client0
            client0.Retrieve(id);
            Assert.Single(env.ConnectionsRaw.ConnectionsClient[0].SendBuffer);
            AssertIsStorePacket(env.ConnectionsRaw.ConnectionsClient[0].SendBuffer[0], EPacket.StoreDataRetrieved, id);
            env.ExecuteSendsClients();
            Assert.Equal(1, CallbackCounter);

            // Retrieve it on client1
            client1.Retrieve(id);
            Assert.Single(env.ConnectionsRaw.ConnectionsClient[1].SendBuffer);
            AssertIsStorePacket(env.ConnectionsRaw.ConnectionsClient[1].SendBuffer[0], EPacket.StoreDataRetrieved, id);
            env.ExecuteSendsClients();
            Assert.Equal(2, CallbackCounter);

            // verify internal call count tracking
            Assert.Single(server.State);
            var sharedState = server.State.First().Value;
            Assert.True(sharedState.RemoteState.ContainsKey(env.Connections.ConnectionsServer[0]));
            Assert.True(sharedState.RemoteState.ContainsKey(env.Connections.ConnectionsServer[1]));

            Assert.Equal(0u, sharedState.InsertCountServer);            
            Assert.Equal(1u, sharedState.RemoteState[env.Connections.ConnectionsServer[0]].InsertCount);
            Assert.Equal(0u, sharedState.RemoteState[env.Connections.ConnectionsServer[1]].InsertCount);

            Assert.Equal(0u, sharedState.RetrieveCountServer);
            Assert.Equal(1u, sharedState.RemoteState[env.Connections.ConnectionsServer[1]].RetrieveCount);
            Assert.Equal(1u, sharedState.RemoteState[env.Connections.ConnectionsServer[1]].RetrieveCount);
        }

        private static void AssertIsStorePacket(byte[] payload, EPacket expectedType, ObjectId expectedId)
        {
            Packet packet = new PacketReader().Read(new ByteReader(new ArraySegment<byte>(payload)));
            Assert.Equal(expectedType, packet.Type);
            Assert.Equal(4, packet.Payload.Count); // uint32
            uint uiMessageId = new ByteReader(packet.Payload).Binary.ReadUInt32();
            Assert.Equal(expectedId, new ObjectId(uiMessageId));
        }
    }
}
