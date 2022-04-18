using Coop.Tests.Persistence;
using Sync.Store;
using Xunit;

namespace Coop.Tests.Sync
{
    public class RemoteStoreServer_Test
    {
        [Fact]
        private void DataIsBroadcastToOtherClients()
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
            Assert.False(server.State[id].IsAvailableEverywhere());
            env.ExecuteSendsClients();
            Assert.True(server.State[id].IsAvailableEverywhere());

            // Object is present and equal in all stores
            Assert.Equal(message, client0.Data[id] as string);
            Assert.Equal(message, client1.Data[id] as string);
            Assert.Equal(message, env.StoreServer.Data[id] as string);
        }

        [Fact]
        private void DataIsReceivedFromClient()
        {
            var env = new TestEnvironment(1);
            var message = "Hello World";

            var client0 = env.StoresClient[0];

            var id = client0.Insert(message);
            Assert.DoesNotContain(id, env.StoreServer.Data);

            env.ExecuteSendsClients();
            Assert.Contains(id, env.StoreServer.Data);
            Assert.IsType<string>(env.StoreServer.Data[id]);
            Assert.Equal(message, env.StoreServer.Data[id] as string);
        }

        [Fact]
        private void OnDistributedIsInvoked()
        {
            var env = new TestEnvironment(2);
            var message = "Hello World";

            var client0 = env.StoresClient[0];
            var client1 = env.StoresClient[1];

            var id = client0.Insert(message);

            ObjectId? handlerArgument = null;
            env.StoreServer.OnObjectAvailable += objectId => { handlerArgument = objectId; };

            // Client0 Add -> Server
            env.ExecuteSendsClients();
            Assert.False(handlerArgument.HasValue);

            // Server Add -> Client1
            env.ExecuteSendsServer();
            Assert.False(handlerArgument.HasValue);

            // Client1 ACK -> Server
            env.ExecuteSendsClients();
            Assert.True(handlerArgument.HasValue);

            // Server ACK -> Client 0
            env.ExecuteSendsServer();
        }

        [Fact]
        private void ServerCanAddObject()
        {
            var env = new TestEnvironment(2);
            var message = "Hello World";

            var client0 = env.StoresClient[0];
            var client1 = env.StoresClient[1];

            var id = env.StoreServer.Insert(message);
            Assert.Contains(id, env.StoreServer.Data);
            Assert.DoesNotContain(id, client0.Data);
            Assert.DoesNotContain(id, client1.Data);

            env.ExecuteSendsServer();
            Assert.Contains(id, env.StoreServer.Data);
            Assert.Contains(id, client0.Data);
            Assert.Contains(id, client1.Data);
        }
    }
}