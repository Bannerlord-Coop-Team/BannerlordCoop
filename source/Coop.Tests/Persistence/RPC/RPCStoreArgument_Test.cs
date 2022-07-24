using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RemoteAction;
using Network;
using Network.Protocol;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using Sync.Call;
using Sync.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Persistence.RPC
{
    [Collection("UsesGlobalPatcher")]
    public class RPCStoreArgument_Test : IDisposable
    {
        private readonly TestEnvironment m_Environment = new TestEnvironment(2, Registry.Client, Registry.Server);
        private readonly RemoteStoreClient m_Store0;
        private readonly RemoteStoreClient m_Store1;
        private readonly RemoteStoreServer m_StoreServer;
        private readonly CoopSyncClient m_Sync0;
        private readonly CoopSyncClient m_Sync1;
        private readonly CoopSyncServer m_SyncServer;
        private readonly RailClient m_Rail0;
        private readonly RailClient m_Rail1;
        private readonly RailServer m_RailServer;
        private readonly Foo m_Foo = new Foo();

        public RPCStoreArgument_Test()
        {
            m_Store0 = m_Environment.StoresClient[0];
            m_Store1 = m_Environment.StoresClient[1];
            m_StoreServer = m_Environment.StoreServer;
            m_Sync0 = new CoopSyncClient(m_Environment.GetClientAccess(0));
            m_Sync1 = new CoopSyncClient(m_Environment.GetClientAccess(1));
            m_SyncServer = new CoopSyncServer(m_Environment.GetServerAccess());
            m_Rail0 = m_Environment.Persistence.Clients[0];
            m_Rail1 = m_Environment.Persistence.Clients[1];
            m_RailServer = m_Environment.Persistence.Server;
        }

        public void Dispose()
        {
            m_Environment.Destroy();
        }

        private class Foo
        {
            public static Dictionary<Guid, List<string>> Calls = new Dictionary<Guid, List<string>> { };
            public static Invokable TestMethodRPC = new Invokable(typeof(Foo).GetMethod(nameof(Foo.TestMethod), BindingFlags.NonPublic | BindingFlags.Static));
            private static void TestMethod(Guid id, string arg)
            {
                if(!Calls.ContainsKey(id))
                {
                    Calls.Add(id, new List<string>() { arg });
                }
                else
                {
                    Calls[id].Add(arg);
                }
            }
        }

        [Fact]
        private void CanBroadcastRPC()
        {
            Guid id = Guid.NewGuid();
            Foo.Calls.Add(id, new List<string> { });
            const string testString = "Hello World";
            Assert.Empty(m_StoreServer.Data);
            m_Sync0.Broadcast(Foo.TestMethodRPC.Id, null, new object[] { id, testString });
            Assert.Contains(m_Store0.Data, pair => pair.Value is string s && s == testString);
            Assert.True(m_Environment.EventQueue.Count == 0); // Filled by server

            // Client 0 -> Server
            UpdateClients();
            Assert.Contains(m_StoreServer.Data, pair => pair.Value is string s && s == testString);
            Assert.True(m_Environment.EventQueue.Count == 1);
            Assert.Empty(Foo.Calls[id]);

            // Server -> Client 1
            UpdateServer();
            Assert.Contains(m_Store1.Data, pair => pair.Value is string s && s == testString);
            Assert.True(m_Environment.EventQueue.Count == 1); // Call is still pending because the ACK for the argument has not yet been sent back to client 0
            Assert.Empty(Foo.Calls[id]);

            // Client 1 ACK -> Server
            UpdateClients();

            // Server will now send out calls because it got all ACKS
            m_Environment.EventQueue.Update(TimeSpan.Zero);
            Assert.True(m_Environment.EventQueue.Count == 0); // Call has been sent out, because the server got all ACKs
            Assert.Empty(Foo.Calls[id]);

            // Server ACK -> Client 0. Doesn't really matter for the RPC
            m_Environment.ExecuteSendsServer();
            Assert.Empty(Foo.Calls[id]);

            // Process the RPC that is pending in railgun & send railgun update
            m_Environment.Persistence.UpdateServer();
            Assert.Empty(Foo.Calls[id]);

            // Railgun will execute the calls immediately when the packet arives
            m_Environment.ExecuteSendsServer();
            Assert.Equal(expected: 2, actual: Foo.Calls[id].Count); // 2 because both clients get the call, including the one that started the broadcast.
        }

        [Fact]
        private void StoreObjectAreRemovedAfterRPC()
        {
            // Make a broadcast, identical to `CanBroadcastRPC`.
            Guid id = Guid.NewGuid();
            Foo.Calls.Add(id, new List<string> { });
            const string testString = "Hello World";
            Assert.Empty(m_StoreServer.Data);
            m_SyncServer.Broadcast(Foo.TestMethodRPC.Id, null, new object[] { id, testString });
            Assert.Single(m_StoreServer.Data);
            Assert.Equal(1, m_Environment.EventQueue.Count);
            Assert.Equal(testString, m_StoreServer.Data.First().Value as string);
            ObjectId objectId = m_StoreServer.Data.First().Key;

            // Server -> Clients StoreAdd
            UpdateServer();
            Assert.Equal(1, m_Environment.EventQueue.Count);

            // Clients ACK -> Server
            UpdateClients();
            Assert.Equal(1, m_Environment.EventQueue.Count);

            // Server -> Client 0. This includes the ACK from Client 1 and the RPC itself.
            UpdateServer();
            Assert.Equal(0, m_Environment.EventQueue.Count);
            Assert.Equal(expected: 2, actual: Foo.Calls[id].Count); // 2 because both clients got the call, including the one that started the broadcast.

            // RPC complete. The clients should send a StoreDataRetrieved because they resolved the RPC argument
            Assert.Single(m_Environment.ConnectionsRaw.ConnectionsClient[0].SendBuffer);
            AssertIsStorePacket(m_Environment.ConnectionsRaw.ConnectionsClient[0].SendBuffer[0], EPacket.StoreDataRetrieved, objectId);
            Assert.Single(m_Environment.ConnectionsRaw.ConnectionsClient[1].SendBuffer);
            AssertIsStorePacket(m_Environment.ConnectionsRaw.ConnectionsClient[1].SendBuffer[0], EPacket.StoreDataRetrieved, objectId);
            UpdateClients();

            // The event queue will remove the unneeded objects on the next update
            m_Environment.EventQueue.Update(TimeSpan.Zero);
            Assert.Empty(m_StoreServer.Data);
            Assert.Single(m_Environment.ConnectionsRaw.ConnectionsServer[0].SendBuffer);
            AssertIsStorePacket(m_Environment.ConnectionsRaw.ConnectionsServer[0].SendBuffer[0], EPacket.StoreRemove, objectId);
            Assert.Single(m_Environment.ConnectionsRaw.ConnectionsServer[1].SendBuffer);
            AssertIsStorePacket(m_Environment.ConnectionsRaw.ConnectionsServer[1].SendBuffer[0], EPacket.StoreRemove, objectId);
        }

        private static void AssertIsStorePacket(byte[] payload, EPacket expectedType, ObjectId expectedId)
        {
            Packet packet = new PacketReader().Read(new ByteReader(new ArraySegment<byte>(payload)));
            Assert.Equal(expectedType, packet.Type);
            Assert.Equal(4, packet.Payload.Count); // uint32
            uint uiMessageId = new ByteReader(packet.Payload).Binary.ReadUInt32();
            Assert.Equal(expectedId, new ObjectId(uiMessageId));
        }

        private void UpdateServer()
        {
            m_Environment.EventQueue.Update(TimeSpan.Zero);
            m_Environment.Persistence.UpdateServer();
            m_Environment.ExecuteSendsServer();
        }

        private void UpdateClients()
        {
            m_Environment.Persistence.UpdateClients();
            m_Environment.ExecuteSendsClients();
        }
    }
}
