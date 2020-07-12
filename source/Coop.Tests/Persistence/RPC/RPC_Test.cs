using System;
using System.Linq;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RPC;
using Coop.Tests.Sync;
using JetBrains.Annotations;
using Network;
using Network.Protocol;
using RailgunNet.Connection.Client;
using Sync;
using Sync.Store;
using Xunit;

namespace Coop.Tests.Persistence.RPC
{
    [Collection(
        "UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class RPC_Test : IDisposable
    {
        public RPC_Test()
        {
            // Init patch
            MethodPatch Patch = new MethodPatch(typeof(Foo)).Intercept(nameof(Foo.SyncedMethod));
            Persistence = m_Environment.Persistence ??
                          throw new Exception("Persistence may not be null. Error in test setup.");
            Persistence.SyncHandlers.Register(
                Patch.Methods,
                m_Environment.GetClientAccess(ClientId0));
            Access = Patch.Methods.First();
            SyncHandler = Persistence.SyncHandlers.Handlers.Find(o => o.MethodAccess == Access);
            if (SyncHandler == null)
            {
                throw new Exception("Error during setup.");
            }
        }

        public void Dispose()
        {
            m_Environment.Destroy();
        }

        private class Foo
        {
            [ThreadStatic] public static string LatestArgument;
            [ThreadStatic] public static int NumberOfCalls;

            public static void SyncedMethod(string sSomeArgument)
            {
                ++NumberOfCalls;
                LatestArgument = sSomeArgument;
            }
        }

        private readonly TestEnvironment m_Environment = new TestEnvironment(
            2,
            Registry.Client,
            Registry.Server);

        [NotNull] private TestPersistence Persistence { get; }
        [NotNull] private MethodAccess Access { get; }

        [NotNull] private MethodCallSyncHandler SyncHandler { get; }

        private const int ClientId0 = 0;
        private const int ClientId1 = 1;

        [Fact]
        private void CallingPatchedMethodInvokesSyncHandler()
        {
            InMemoryConnection conClientToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            string sMessage = "Hello World";
            int iNumberOfExpectedCalls = 0;

            // Verify initial state
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);
            Assert.Empty(SyncHandler.Stats.History);
            Assert.Empty(conClientToServer.SendBuffer);

            // Call method
            Foo.SyncedMethod(sMessage);
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);

            // Verify that the sync handler was called
            Assert.Single(SyncHandler.Stats.History);
            MethodCallSyncHandler.Statistics.Trace trace = SyncHandler.Stats.History.Peek();
            Assert.Equal(Persistence.Rooms[ClientId0].Tick, trace.Tick);
            Assert.Equal(
                Argument.Null,
                trace.Call.Instance); // Since it's a static call the instance is null

            // Verify the RPC argument
            Assert.Single(trace.Call.Arguments);
            Argument arg0 = trace.Call.Arguments[ClientId0];
            Assert.Equal(
                EventArgType.StoreObjectId,
                arg0.EventType); // strings are always put into the store
            Assert.True(arg0.StoreObjectId.HasValue);
            ObjectId messageId = arg0.StoreObjectId.Value;

            // Verify arg0 was put into store
            RemoteStore store = m_Environment.StoresClient[ClientId0];
            Assert.Single(store.Data);
            Assert.True(store.Data.ContainsKey(messageId));
            object storeData = store.Data[messageId];
            Assert.NotNull(storeData);
            Assert.IsType<string>(storeData);
            string sMessageFromStore = (string) storeData;
            Assert.Equal(sMessage, sMessageFromStore);

            // Adding it to the store sent a StoreAdd message containing the serialized string
            Assert.Single(conClientToServer.SendBuffer);
            byte[] payload = conClientToServer.SendBuffer[0];
            EPacket eType = PacketReader.DecodePacketType(payload[0]);
            Assert.Equal(EPacket.StoreAdd, eType);

            // Verify the payload in StoreAdd
            Packet packet =
                new PacketReader().Read(new ByteReader(new ArraySegment<byte>(payload)));
            long expectedPayloadSize = TestUtils.GetSerializedSize(sMessage);
            Assert.Equal(expectedPayloadSize, packet.Payload.Count);
        }

        [Fact]
        private void EventIsReceivedByServer()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            InMemoryConnection conClient0 =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];

            // Call method
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            client0.Update();

            // Verify server side state before receiving the event
            Assert.Equal(0, m_Environment.EventQueue.Count);
            Assert.Empty(m_Environment.StoreServer.Data);

            // Receive
            conClient0.ExecuteSends();
            Assert.Equal(1, m_Environment.EventQueue.Count);
            Assert.Single(m_Environment.StoreServer.Data);

            // Verify the argument was received by the server
            MethodCallSyncHandler.Statistics.Trace trace = SyncHandler.Stats.History.Peek();
            ObjectId messageId = trace.Call.Arguments[0].StoreObjectId.Value;
            Assert.True(m_Environment.StoreServer.Data.ContainsKey(messageId));
        }

        [Fact]
        private void ServerRelaysStoreAdd()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            RailClient client1 = Persistence.Clients[ClientId1];
            InMemoryConnection conClient0ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            InMemoryConnection conServerToClient0 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId0];
            InMemoryConnection conClient1ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId1];
            InMemoryConnection conServerToClient1 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId1];

            // Call method and send event to server
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            client0.Update();
            conClient0ToServer.ExecuteSends();
            MethodCallSyncHandler.Statistics.Trace trace = SyncHandler.Stats.History.Peek();
            ObjectId messageId = trace.Call.Arguments[0].StoreObjectId.Value;

            // The server relayed the StoreAdd to client 1
            Assert.Single(conServerToClient1.SendBuffer);
            byte[] payloadAdd = conServerToClient1.SendBuffer[0];
            EPacket eTypeAdd = PacketReader.DecodePacketType(payloadAdd[0]);
            Assert.Equal(EPacket.StoreAdd, eTypeAdd);
            Assert.Empty(conServerToClient0.SendBuffer); // But nothing back to client 0

            // Let the client 1 receive the StoreAdd. Client 1 returns an ACK
            conServerToClient1.ExecuteSends();
            Assert.Single(conClient1ToServer.SendBuffer);
            byte[] payloadAck = conClient1ToServer.SendBuffer[0];
            EPacket eTypeAck = PacketReader.DecodePacketType(payloadAck[0]);
            Assert.Equal(EPacket.StoreAck, eTypeAck);

            // Receive the ACK on the server. The server will then ACK back to client 0
            Assert.Empty(conServerToClient0.SendBuffer);
            conClient1ToServer.ExecuteSends();
            Assert.Single(conServerToClient0.SendBuffer);
        }

        [Fact]
        private void ServerWaitsUntilAllArgumentsAreDistributed()
        {
            RailClient client0 = Persistence.Clients[ClientId0];
            RailClient client1 = Persistence.Clients[ClientId1];
            InMemoryConnection conClient0ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            InMemoryConnection conServerToClient0 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId0];
            InMemoryConnection conClient1ToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId1];
            InMemoryConnection conServerToClient1 =
                m_Environment.ConnectionsRaw.ConnectionsServer[ClientId1];

            // Call method and send event to server
            string sMessage = "Hello World";
            Foo.SyncedMethod(sMessage);
            client0.Update();
            conClient0ToServer.ExecuteSends();
            Assert.Equal(1, m_Environment.EventQueue.Count);

            // Updating the event queue will not broadcast anything because the argument was not yet acknowledged by client 1
            m_Environment.EventQueue.Update(TimeSpan.Zero);
            Assert.Equal(1, m_Environment.EventQueue.Count);
            Assert.Single(conServerToClient1.SendBuffer); // StoreAdd to client 1
            Assert.Empty(conServerToClient0.SendBuffer);

            // Let the client 1 receive the StoreAdd & return ACK to server
            conServerToClient1.ExecuteSends();
            Assert.Single(conClient1ToServer.SendBuffer);
            conClient1ToServer.ExecuteSends();

            // Event can now be broadcast
            Assert.Equal(1, m_Environment.EventQueue.Count);
            m_Environment.EventQueue.Update(TimeSpan.Zero);
            Assert.Equal(0, m_Environment.EventQueue.Count);
        }

        [Fact]
        private void SyncHandlersGeneratesEvent()
        {
            // Call method
            string sMessage = "Hello World";
            int iNumberOfExpectedCalls = 0;
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);
            Assert.Empty(SyncHandler.Stats.History);
            Foo.SyncedMethod(sMessage);
            Assert.Equal(iNumberOfExpectedCalls, Foo.NumberOfCalls);

            // Verify the SyncHandler was called at all
            Assert.Single(SyncHandler.Stats.History);

            // There should be an outgoing event in the clients queue. We should be able to observe the event being sent on the next update.
            RailClient client0 = Persistence.Clients[ClientId0];
            InMemoryConnection conClientToServer =
                m_Environment.ConnectionsRaw.ConnectionsClient[ClientId0];
            Assert.Single(
                conClientToServer.SendBuffer); // There's already a StoreAdd packet in there
            client0.Update();
            Assert.Equal(2, conClientToServer.SendBuffer.Count);
            EPacket eType = PacketReader.DecodePacketType(conClientToServer.SendBuffer.Last()[0]);
            Assert.Equal(EPacket.Persistence, eType);
        }
    }
}
